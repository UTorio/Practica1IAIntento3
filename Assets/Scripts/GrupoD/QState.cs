using NavigationDJIA.World;
using System;
/// <summary>
/// TODO(alumno):
/// Define el "estado" que usará la Tabla Q para identificar cada situación del agente.
/// 
/// El estado debe contener toda la información necesaria para que el agente pueda
/// tomar decisiones informadas. Tú decides qué características incluir según lo
/// que consideres relevante para resolver el problema.
/// 
/// Ejemplos típicos de información que puede formar un estado:
///   - Posición del agente en la grid.
///   - Posición del otro personaje (enemigo).
///   - Distancia relativa entre agente y enemigo.
///   - Si hay muros en direcciones cercanas.
///   - Cualquier otro dato que consideres útil.
/// 
/// En este ejercicio te damos un ejemplo simple basado únicamente en las posiciones
/// del agente y del oponente. Puedes usarlo tal cual o ampliarlo.
/// 
/// IMPORTANTE: 
///  El estado debe poder convertirse a una clave única (string) mediante ToKey(),
///  ya que esa clave se usará como índice en la TablaQ y en el archivo CSV.
/// </summary>

namespace GrupoD
{
    public sealed class QState
    {
        private const int MaxDepth = 12;   // hasta dónde mira el BFS

        // buffers reutilizables (evitan generar basura en cada paso)
        private static int[] _distA, _distO, _first, _queue;
        private static readonly int[] _safe = new int[4];
        private static int _w, _h;

        // direcciones: 0=Up (y+1), 1=Down (y-1), 2=Left (x-1), 3=Right (x+1)
        private static readonly int[] StepX = { 0, 0, -1, 1 };
        private static readonly int[] StepY = { 1, -1, 0, 0 };

        private readonly int _sx, _sy;   // lado del jugador: -1, 0 o 1 en cada eje
        private readonly bool _terminal;
        private readonly int[] _level = new int[4];   // 0=muro, 1=trampa, 2=poco, 3=medio, 4=mucho
        private readonly int _dangerBucket;           // 0=pegado al jugador ... 3=lejos

        public QState(CellInfo agent, CellInfo other, WorldInfo world)
        {
            if (agent.Equals(other)) { _terminal = true; return; }

            EnsureBuffers(world.WorldSize.x, world.WorldSize.y);

            Bfs(world, agent.x, agent.y, _distA, _first, MaxDepth);
            Bfs(world, other.x, other.y, _distO, null, MaxDepth + 1);

            Array.Clear(_safe, 0, 4);
            for (int idx = 0; idx < _w * _h; idx++)
            {
                int da = _distA[idx];
                if (da <= 0) continue;                        // inalcanzable o la propia celda
                int dO = _distO[idx];
                if (dO == -1 || da < dO) _safe[_first[idx]]++; // el zombi llega antes que el jugador
            }

            for (int i = 0; i < 4; i++)
            {
                CellInfo n = world[agent.x + StepX[i], agent.y + StepY[i]];
                if (n == null || !n.Walkable) _level[i] = 0;
                else if (_safe[i] == 0) _level[i] = 1;
                else if (_safe[i] <= 3) _level[i] = 2;
                else if (_safe[i] <= 10) _level[i] = 3;
                else if (_safe[i] <= 30) _level[i] = 4;
                else _level[i] = 5;

            }

            int dd = _distO[agent.x * _h + agent.y];   // pasos del jugador hasta el zombi
            _dangerBucket = dd == -1 ? 3 : dd <= 1 ? 0 : dd == 2 ? 1 : dd <= 4 ? 2 : 3;
        }

        private static void EnsureBuffers(int w, int h)
        {
            if (_distA != null && w == _w && h == _h) return;
            _w = w; _h = h;
            _distA = new int[w * h]; _distO = new int[w * h];
            _first = new int[w * h]; _queue = new int[w * h];
        }

        private static void Bfs(WorldInfo world, int sx, int sy, int[] dist, int[] first, int maxDepth)
        {
            Array.Fill(dist, -1);
            int head = 0, tail = 0;
            int start = sx * _h + sy;
            dist[start] = 0;
            _queue[tail++] = start;

            while (head < tail)
            {
                int cur = _queue[head++];
                int d = dist[cur];
                if (d >= maxDepth) continue;
                int cx = cur / _h, cy = cur % _h;

                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + StepX[i], ny = cy + StepY[i];
                    if (nx < 0 || ny < 0 || nx >= _w || ny >= _h) continue;
                    int ni = nx * _h + ny;
                    if (dist[ni] != -1) continue;
                    CellInfo c = world[nx, ny];
                    if (c == null || !c.Walkable) continue;

                    dist[ni] = d + 1;
                    if (first != null) first[ni] = (d == 0) ? i : first[cur];
                    _queue[tail++] = ni;
                }
            }
        }

        public string ToKey()
        {
            if (_terminal) return "T";   // estado terminal propio, nunca se confunde con uno real
            return $"{_level[0]}{_level[1]}{_level[2]}{_level[3]}|{_dangerBucket}|{_sx}{_sy}";
        }
    }
}