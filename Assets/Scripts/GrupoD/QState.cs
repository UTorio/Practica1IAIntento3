using NavigationDJIA.World;

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
    private static readonly int[] DX4 = { 0, 0, 1, -1 };   // Up, Down, Right, Left
        private static readonly int[] DY4 = { 1, -1, 0, 0 };
 
        // Menos casillas "ganadas" al perseguidor que esto = callejón
        private const int TrappedThreshold = 6;
 
        // Caché por mundo: consultar world[x, y] es caro, así que se hace una vez por mapa
        private static WorldInfo _cachedWorld;
        private static bool[,] _walkable;
        private static int[,] _distChaser;
        private static int[,] _distTmp;
        private static int _w, _h;
        private static int[] _queueBuf;   // cola del BFS (cada casilla se guarda como x * alto + y)
 
        public int DistBucket { get; }
        public int[] DirCat { get; }
 
        public QState(CellInfo agent, CellInfo other, WorldInfo world)
        {
            EnsureGrid(world);
            DirCat = new int[4];
 
            // Estado terminal (capturado): no hace falta calcular nada
            if (agent.x == other.x && agent.y == other.y)
            {
                DistBucket = 0;
                return;
            }
 
            // Distancias por camino desde el perseguidor a todas las casillas
            Bfs(other.x, other.y, _distChaser);
            int d0 = _distChaser[agent.x, agent.y];
            if (d0 < 0) d0 = _w * _h;
            DistBucket = Bucket(d0);
 
            for (int d = 0; d < 4; d++)
            {
                int nx = agent.x + DX4[d];
                int ny = agent.y + DY4[d];
 
                if (nx < 0 || ny < 0 || nx >= _w || ny >= _h || !_walkable[nx, ny])
                {
                    DirCat[d] = 0;   // bloqueada
                    continue;
                }
 
                int dn = _distChaser[nx, ny];
                if (dn >= 0 && dn < d0)
                {
                    DirCat[d] = 1;   // me acerco
                    continue;
                }
 
                // Me alejo: ¿cuánto espacio tengo allí? Cuento las casillas que alcanzo
                // antes que el perseguidor si salgo en esa dirección.
                Bfs(nx, ny, _distTmp);
                int territory = 0;
                for (int x = 0; x < _w; x++)
                {
                    for (int y = 0; y < _h; y++)
                    {
                        int mine = _distTmp[x, y];
                        if (mine < 0) continue;
                        int his = _distChaser[x, y];
                        if (his < 0 || mine + 1 < his) territory++;
                    }
                }
 
                DirCat[d] = territory < TrappedThreshold ? 2 : 3;
            }
        }
 
        public string ToKey()
        {
            return $"{DistBucket}|{DirCat[0]}{DirCat[1]}{DirCat[2]}{DirCat[3]}";
        }
 
        private static int Bucket(int d)
        {
            if (d <= 4) return d;    // 1..4
            if (d <= 6) return 5;
            if (d <= 9) return 6;
            return 7;
        }
 
        private static void EnsureGrid(WorldInfo world)
        {
            if (ReferenceEquals(world, _cachedWorld) && _walkable != null)
                return;
 
            _cachedWorld = world;
            _w = world.WorldSize.x;
            _h = world.WorldSize.y;
            _walkable = new bool[_w, _h];
            _distChaser = new int[_w, _h];
            _distTmp = new int[_w, _h];
            _queueBuf = new int[_w * _h];
 
            for (int x = 0; x < _w; x++)
            {
                for (int y = 0; y < _h; y++)
                {
                    CellInfo cell = world[x, y];
                    _walkable[x, y] = cell != null && cell.Walkable;
                }
            }
        }
 
        // BFS: pasos mínimos desde (sx, sy) a cada casilla esquivando muros (-1 = inalcanzable)
        private static void Bfs(int sx, int sy, int[,] dist)
        {
            for (int x = 0; x < _w; x++)
                for (int y = 0; y < _h; y++)
                    dist[x, y] = -1;
 
            int head = 0;
            int tail = 0;
            dist[sx, sy] = 0;
            _queueBuf[tail++] = sx * _h + sy;
 
            while (head < tail)
            {
                int cell = _queueBuf[head++];
                int cx = cell / _h;
                int cy = cell % _h;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + DX4[d];
                    int ny = cy + DY4[d];
                    if (nx < 0 || ny < 0 || nx >= _w || ny >= _h) continue;
                    if (!_walkable[nx, ny] || dist[nx, ny] != -1) continue;
 
                    dist[nx, ny] = dist[cx, cy] + 1;
                    _queueBuf[tail++] = nx * _h + ny;
                }
            }
        }
    
    }
}