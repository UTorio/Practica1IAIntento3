using NavigationDJIA.World;
using QMind;
using QMind.Interfaces;

namespace GrupoA
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

            return nextPosition;
        }

        private string BuildStateKey(CellInfo agent, CellInfo other, WorldInfo _worldInfo)
        {
            var state = new QState(agent, other);
            return state.ToKey();
        }

        private CellInfo ApplyAction(CellInfo agentCell, QAction action)
        {
            switch (action)
            {
                case QAction.Up:
                    return _worldInfo[agentCell.x, agentCell.y + 1];
                
                case QAction.Down:
                    return _worldInfo[agentCell.x, agentCell.y - 1];
                
                case QAction.Right:
                    return _worldInfo[agentCell.x + 1, agentCell.y];
                
                case QAction.Left:
                    return _worldInfo[agentCell.x - 1, agentCell.y];
                
                default:
                    return _worldInfo[agentCell.x, agentCell.y];
            }
        }
    }
}