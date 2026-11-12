using System;
using NavigationDJIA.Interfaces;
using NavigationDJIA.World;
using QMind;
using QMind.Interfaces;
using Math = UnityEngine.Mathf;

namespace GrupoD
{
    public class QMindTrainer : IQMindTrainer
    {
        private QMindTrainerParams _params;
        private WorldInfo _worldInfo;
        INavigationAlgorithm _navigationAlgorithm;

        private QTableStorage _qStorage;
        private QTable _qTable;

        private CellInfo _agentPosition;
        private CellInfo _otherPosition;

        private float _return;
        private float _returnAveraged;
        private System.Random _random = new System.Random();

        // --- Ajustes de entrenamiento ---
        private const int MaxStepsPerEpisode = 300;   // tope de pasos por episodio
        private const float EpsilonMin = 0.002f;       // epsilon nunca baja de esto
        private const float EpsilonDecay = 0.998f;    // epsilon = params.epsilon * decay^episodio
        private const int SaveEvery = 50;             // guarda la tabla cada N episodios
        private const int LogEvery = 50;             // log de pasos medios cada N episodios

        private float _epsilon;
        private bool[,] _walkable;     // casillas transitables del mapa actual (caché)
        private int[,] _bfsDist;        // buffer reutilizable para el BFS
        private readonly System.Collections.Generic.Queue<(int x, int y)> _bfsQueue =
            new System.Collections.Generic.Queue<(int x, int y)>();
        private int _stepsWindow;
        private int _episodesWindow;

        #region IQMindTrainer implementation
        public int CambioMapa = 500;
        public CellInfo AgentPosition => _agentPosition;
        public CellInfo OtherPosition => _otherPosition;

        public int CurrentEpisode { get; private set; }
        public int CurrentStep { get; private set; }

        public float Return => _return;
        public float ReturnAveraged => _returnAveraged;

        public event EventHandler OnEpisodeStarted;
        public event EventHandler OnEpisodeFinished;

        #endregion

        public void Initialize(QMindTrainerParams qMindTrainerParams, WorldInfo worldInfo, INavigationAlgorithm navigationAlgorithm)
        {
            _params = qMindTrainerParams;
            _worldInfo = worldInfo;
            _navigationAlgorithm = navigationAlgorithm;
            _navigationAlgorithm.Initialize(worldInfo);
            _walkable = null;

            _qStorage = new QTableStorage("TablaQ.csv");
            _qTable = new QTable(_qStorage);

            CurrentEpisode = 0;
            StartNewEpisode();
        }

        private void StartNewEpisode()
        {
            if (CurrentEpisode > 1 && (CurrentEpisode - 1) % CambioMapa == 0)
            {
                _worldInfo = RandomizeArena(true);
                _navigationAlgorithm.Initialize(_worldInfo);
                _walkable = null;   // los muros han cambiado: invalida la caché
            }
            CurrentEpisode++;
            CurrentStep = 0;
            _return = 0f;
            _returnAveraged = 0f;

            // Epsilon decreciente: explora mucho al principio y explota después
            _epsilon = Math.Max(EpsilonMin, (float)_params.epsilon * Math.Pow(EpsilonDecay, CurrentEpisode));

            _agentPosition = _worldInfo.RandomCell();
            _otherPosition = _worldInfo.RandomCell();

            // Evita empezar capturado o con el perseguidor pegado
            for (int i = 0; i < 20 && ManhattanDistance(_agentPosition, _otherPosition) < 4; i++)
            {
                _otherPosition = _worldInfo.RandomCell();
            }

            OnEpisodeStarted?.Invoke(this, EventArgs.Empty);
        }

        private void EndEpisode()
        {
            // Estadística para ver si realmente aprende
            _stepsWindow += CurrentStep;
            _episodesWindow++;
            if (CurrentEpisode % LogEvery == 0)
            {
                UnityEngine.Debug.Log($"Episodio {CurrentEpisode} | pasos medios: {(float)_stepsWindow / _episodesWindow:F1} | epsilon: {_epsilon:F3}");
                _stepsWindow = 0;
                _episodesWindow = 0;
            }

            bool last = _params.episodes > 0 && CurrentEpisode >= _params.episodes;

            // Guardar el CSV cada episodio es muy lento: se guarda cada SaveEvery y al final
            if (CurrentEpisode % SaveEvery == 0 || last)
            {
                _qTable.SaveToCsv();
            }

            OnEpisodeFinished?.Invoke(this, EventArgs.Empty);

            if (last)
            {
                return;
            }

            StartNewEpisode();
        }

        public void DoStep(bool train)
        {
            CellInfo oldAgent = _agentPosition;
            CellInfo oldOther = _otherPosition;

            // Estado actual del agente
            string stateKey = BuildStateKey(oldAgent, oldOther, _worldInfo);

            // Selecciona la acción a realizar
            QAction action = ChooseAction(stateKey, train);

            // Nuevos estados del agente y del oponente
            CellInfo newAgentPos = ApplyAction(oldAgent, action);
            CellInfo newOtherPos = MoveOpponent(oldOther, newAgentPos.Walkable ? newAgentPos : oldAgent);

            // Nuevo estado
            string nextStateKey = BuildStateKey(newAgentPos, newOtherPos, _worldInfo);

            bool terminal = IsTerminalState(newAgentPos, newOtherPos);

            // Calcula la recompensa
            float reward = ComputeReward(newAgentPos, newOtherPos);

            if (train)
            {
                UpdateQ(stateKey, action, reward, nextStateKey, terminal);
            }

            // Actualiza las posiciones
            _agentPosition = newAgentPos;
            _otherPosition = newOtherPos;

            // Estadísticas de recompensas
            CurrentStep++;
            _return += reward;
            _returnAveraged = (_returnAveraged * (CurrentStep - 1) + reward) / CurrentStep;

            // Fin de episodio: captura o tope de pasos
            if (terminal || CurrentStep >= MaxStepsPerEpisode)
            {
                EndEpisode();
            }
        }

        #region Parte a implementar por el alumno

        private string BuildStateKey(CellInfo agent, CellInfo other, WorldInfo worldInfo)
        {
            var state = new QState(agent, other, worldInfo);
            return state.ToKey();
        }

        private QAction ChooseAction(string stateKey, bool train)
        {
            if (!train)
            {
                return _qTable.GetBestAction(stateKey);
            }

            float eps = _params.epsilon;
            if (_params.episodes > 0)
            {
                float progress = CurrentEpisode / (float)_params.episodes;
                eps = Math.Max(0.02f, _params.epsilon * (1f - progress));
            }

            double r = _random.NextDouble();
            if (r < eps)
            {
                // Acción aleatoria
                Array actions = Enum.GetValues(typeof(QAction));
                return (QAction)actions.GetValue(_random.Next(actions.Length));
            }

            return _qTable.GetBestAction(stateKey);
        }

        /// <summary>
        /// Q(s,a) = (1 - alpha) * Q(s,a) + alpha * (reward + gamma * max_a' Q(s',a')).
        /// En un estado terminal no hay futuro: el objetivo es solo la recompensa.
        /// </summary>
        private void UpdateQ(string stateKey, QAction action, float reward, string nextStateKey, bool terminal)
        {
            float oldQ = _qTable.GetQ(stateKey, action);
            float target = reward;
            if (!terminal)
            {
                float maxQNext = _qTable.GetMaxQ(nextStateKey);
                target = reward + _params.gamma * maxQNext;
            }
            float newQ = (1 - _params.alpha) * oldQ + _params.alpha * target;
            _qTable.SetQ(stateKey, action, newQ);
        }

        /// <summary>
        /// Recompensa para un zombi que huye. Escala moderada:
        /// morir = -100; el resto de pasos oscilan aproximadamente entre -1 y +0.8.
        /// </summary>
        private float ComputeReward(CellInfo agent, CellInfo other)
        {
            if (IsTerminalState(agent, other)) return -100f;
            float d = agent.Distance(other, CellInfo.DistanceType.Manhattan);
            float tier = d >= 8 ? 1f      // far
                       : d >= 4 ? 0.5f    // medium
                       : 0f;              // close
            return 1f + tier;
        }

        private int ManhattanDistance(CellInfo a, CellInfo b)
        {
            return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
        }

        private int BoardWidth() { return _worldInfo.WorldSize.x; }
        private int BoardHeight() { return _worldInfo.WorldSize.y; }

        private static readonly int[] DX = { 0, 0, 1, -1 };
        private static readonly int[] DY = { 1, -1, 0, 0 };

        // Guarda qué casillas son transitables (una sola vez por mapa).
        // Consultar _worldInfo[x, y] es caro, así que todo lo demás trabaja sobre esta caché.
        private void EnsureGrid()
        {
            int w = BoardWidth();
            int h = BoardHeight();

            if (_walkable != null && _walkable.GetLength(0) == w && _walkable.GetLength(1) == h)
                return;

            _walkable = new bool[w, h];
            _bfsDist = new int[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    _walkable[x, y] = _worldInfo[x, y].Walkable;
        }

        private int CountFreeNeighbours(CellInfo c)
        {
            int n = 0;
            if (IsFree(c.x + 1, c.y)) n++;
            if (IsFree(c.x - 1, c.y)) n++;
            if (IsFree(c.x, c.y + 1)) n++;
            if (IsFree(c.x, c.y - 1)) n++;
            return n;
        }

        private bool IsFree(int x, int y)
        {
            CellInfo cell = _worldInfo[x, y];
            return cell != null && cell.Walkable;
        }

        // BFS: pasos mínimos desde 'origin' hasta cada casilla esquivando muros (-1 = inalcanzable).
        // Devuelve un buffer compartido: leer lo que se necesite antes de volver a llamarlo.
        private int[,] DistancesFrom(CellInfo origin)
        {
            EnsureGrid();
            int w = _walkable.GetLength(0);
            int h = _walkable.GetLength(1);

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    _bfsDist[x, y] = -1;

            _bfsQueue.Clear();
            _bfsDist[origin.x, origin.y] = 0;
            _bfsQueue.Enqueue((origin.x, origin.y));

            while (_bfsQueue.Count > 0)
            {
                var (cx, cy) = _bfsQueue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + DX[d];
                    int ny = cy + DY[d];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    if (!_walkable[nx, ny] || _bfsDist[nx, ny] != -1) continue;

                    _bfsDist[nx, ny] = _bfsDist[cx, cy] + 1;
                    _bfsQueue.Enqueue((nx, ny));
                }
            }

            return _bfsDist;
        }

        // Distancia hasta una casilla en un mapa de BFS; si es inalcanzable, devuelve el máximo posible
        private int PathDist(int[,] bfsMap, CellInfo c)
        {
            int d = bfsMap[c.x, c.y];
            return d < 0 ? BoardWidth() * BoardHeight() : d;
        }

        /// <summary>
        /// Fin de episodio: agente y oponente en la misma celda.
        /// Se comparan coordenadas porque WorldInfo crea un CellInfo nuevo en cada consulta
        /// y comparar referencias podría no detectar nunca la captura.
        /// </summary>
        private bool IsTerminalState(CellInfo agent, CellInfo other)
        {
            return agent.x == other.x && agent.y == other.y;
        }

        private CellInfo ApplyAction(CellInfo agentCell, QAction action)
        {
            int nx = agentCell.x;
            int ny = agentCell.y;

            switch (action)
            {
                case QAction.Up:    ny += 1; break;
                case QAction.Down:  ny -= 1; break;
                case QAction.Right: nx += 1; break;
                case QAction.Left:  nx -= 1; break;
                case QAction.Stay:  return agentCell;
            }

            CellInfo target = _worldInfo[nx, ny];
            return (target != null && target.Walkable) ? target : agentCell;
        }

        private CellInfo MoveOpponent(CellInfo opponent, CellInfo target)
        {
            var path = _navigationAlgorithm.GetPath(opponent, target, 1);
            if (path.Length > 0)
                return path[0];

            return opponent;
        }

        // Aleatoriza el mapa cada cierto número de episodios para entrenar en entornos distintos
        private WorldInfo RandomizeArena(bool resize)
        {
            var root = Components.WorldManager.Instance.gameObject;
            var walls = new System.Collections.Generic.List<UnityEngine.Transform>();
            UnityEngine.Transform floor = null;
            foreach (var t in root.GetComponentsInChildren<UnityEngine.Transform>())
            {
                if (t.CompareTag("Wall")) walls.Add(t);
                else if (t.CompareTag("Floor")) floor = t;
            }

            // Tamaño nuevo (una sola vez, no en cada intento)
            if (resize && floor != null)
            {
                int newW = _random.Next(15, 26);   // 15..25
                int newH = _random.Next(15, 26);
                floor.localScale = new UnityEngine.Vector3(newW / 20f, 1f, newH / 20f);
            }
            UnityEngine.Physics.SyncTransforms();

            // Mundo "de medida" para saber tamaño y convertir celdas a posiciones
            var sizing = new WorldInfo(root, AllowedDirections.FourDirections, 1.0f);
            int w = sizing.WorldSize.x, h = sizing.WorldSize.y;

            WorldInfo candidate = null;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                foreach (var wall in walls)
                {
                    int cx = _random.Next(3, w - 3);
                    int cy = _random.Next(3, h - 3);
                    UnityEngine.Vector3 p = sizing.ToWorldPosition(sizing[cx, cy]);
                    wall.position = new UnityEngine.Vector3(p.x, wall.position.y, p.z);
                    wall.rotation = UnityEngine.Quaternion.Euler(0, _random.Next(2) * 90f, 0);
                }

                UnityEngine.Physics.SyncTransforms();
                candidate = new WorldInfo(root, AllowedDirections.FourDirections, 1.0f);
                if (IsArenaValid(candidate)) return candidate;
            }

            UnityEngine.Debug.LogWarning("No encontré un layout válido en 100 intentos, uso el último");
            return candidate;
        }

        private bool IsArenaValid(WorldInfo world)
        {
            int w = world.WorldSize.x, h = world.WorldSize.y;
            int free = 0, sx = -1, sy = -1;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (world[x, y].Walkable) { free++; if (sx < 0) { sx = x; sy = y; } }
            if (free == 0 || free < 0.7f * w * h) return false;

            var seen = new bool[w, h];
            var q = new System.Collections.Generic.Queue<(int x, int y)>();
            q.Enqueue((sx, sy)); seen[sx, sy] = true;
            int reached = 0;
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            while (q.Count > 0)
            {
                var (cx, cy) = q.Dequeue(); reached++;
                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + dx[i], ny = cy + dy[i];
                    if (nx >= 0 && ny >= 0 && nx < w && ny < h && !seen[nx, ny] && world[nx, ny].Walkable)
                    { seen[nx, ny] = true; q.Enqueue((nx, ny)); }
                }
            }
            return reached == free;
        }

        #endregion
    }
}