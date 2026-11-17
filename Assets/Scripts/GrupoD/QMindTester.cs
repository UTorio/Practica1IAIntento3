using NavigationDJIA.World;
using QMind;
using QMind.Interfaces;

namespace GrupoD
{
    public class QMindTester : IQMind
    {
        private WorldInfo _worldInfo;
        private QTableStorage _qStorage;
        private QTable _qTable;

        public void Initialize(WorldInfo worldInfo)
        {
            _worldInfo = worldInfo;

            _qStorage = new QTableStorage("TablaQ.csv");
            _qTable = new QTable(_qStorage);
        }

        public CellInfo GetNextStep(CellInfo currentPosition, CellInfo otherPosition)
        {
            string stateKey = BuildStateKey(currentPosition, otherPosition, _worldInfo);

            QAction bestAction = _qTable.GetBestAction(stateKey);

            CellInfo nextPosition = ApplyAction(currentPosition, bestAction);

            return nextPosition ?? currentPosition;
        }

        private string BuildStateKey(CellInfo agent, CellInfo other, WorldInfo _worldInfo)
        {
            var state = new QState(agent, other, _worldInfo);
            return state.ToKey();
        }

        private CellInfo ApplyAction(CellInfo agentCell, QAction action)
        {
            int nx = agentCell.x;
            int ny = agentCell.y;

            switch (action)
            {
                case QAction.Up: ny += 1; break;
                case QAction.Down: ny -= 1; break;
                case QAction.Right: nx += 1; break;
                case QAction.Left: nx -= 1; break;
                case QAction.Stay: return agentCell;
            }

            CellInfo target = _worldInfo[nx, ny];
            return (target != null && target.Walkable) ? target : agentCell;
        }
    }
}