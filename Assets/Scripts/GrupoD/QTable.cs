using System;
using QMind;

namespace GrupoD
{
    public class QTable
    {
        private readonly QTableStorage _storage;
        private readonly string[] _actionNames;

        public QTable(QTableStorage storage)
        {
            _storage = storage;
            _actionNames = Enum.GetNames(typeof(QAction));
        }

        private void EnsureState(string stateKey)
        {
            if (!_storage.Data.ContainsKey(stateKey))
            {
                _storage.Data[stateKey] = new float[_actionNames.Length];
            }
        }

        /// <summary>
        /// TODO(alumno):
        /// Devuelve el valor Q(s, a) correspondiente al estado y acción indicados.
        /// 
        /// Pasos recomendados:
        ///  1. Asegúrate de que el estado existe llamando a EnsureState(stateKey).
        ///  2. Convierte la acción en un índice del array:
        ///        int index = (int)action;
        ///  3. Devuelve el valor almacenado en:
        ///        _storage.Data[stateKey][index]
        /// </summary>
        public float GetQ(string stateKey, QAction action)
        {
            EnsureState(stateKey);
            return _storage.Data[stateKey][(int)action];
        }

        /// <summary>
        /// TODO(alumno):
        /// Asigna el valor Q(s, a) para el estado y acción indicados.
        /// 
        /// Pasos recomendados:
        ///  1. Asegúrate de que el estado existe llamando a EnsureState(stateKey).
        ///  2. Convierte la acción en un índice del array:
        ///        int index = (int)action;
        ///  3. Guarda el valor recibido en:
        ///        _storage.Data[stateKey][index] = value;
        /// </summary>
        public void SetQ(string stateKey, QAction action, float value)
        {
            EnsureState(stateKey);
            _storage.Data[stateKey][(int)action] = value;
        }

        /// <summary>
        /// TODO(alumno):
        /// Devuelve el valor máximo max_a Q(s, a) para el estado indicado.
        /// 
        /// Este método se usa en la actualización de Q-Learning:
        ///   maxQNext = GetMaxQ(nextStateKey)
        /// 
        /// Pasos recomendados:
        ///  1. Asegúrate de que el estado existe llamando a EnsureState(stateKey).
        ///  2. Obtén el array de Q-values:
        ///        var qValues = _storage.Data[stateKey];
        ///  3. Recorre el array buscando el valor máximo y devuélvelo.
        /// </summary>
        public float GetMaxQ(string stateKey)
        {
            EnsureState(stateKey);
            float[] qValues = _storage.Data[stateKey];
            float maxQ = qValues[0];
            for(int i = 1; i < qValues.Length; i++)
            {
                if (qValues[i] > maxQ) maxQ = qValues[i];
            }
            return maxQ;
        }

        /// <summary>
        /// TODO(alumno):
        /// Devuelve la mejor acción para el estado indicado:
        ///    argmax_a Q(s, a)
        /// 
        /// Este método se usa para:
        ///   - Política greedy (explotar lo aprendido).
        ///   - Parte "explotar" de la política ε-greedy.
        /// 
        /// Pasos recomendados:
        ///  1. Asegúrate de que el estado existe llamando a EnsureState(stateKey).
        ///  2. Obtén el array de Q-values:
        ///        var qValues = _storage.Data[stateKey];
        ///  3. Recorre el array buscando el índice del valor máximo.
        ///  4. Convierte ese índice a QAction:
        ///        return (QAction)bestIndex;
        /// </summary>
        public QAction GetBestAction(string stateKey)
        {
            EnsureState(stateKey);
            float[] qValues = _storage.Data[stateKey];
            int bestIndex = 0;
            for(int i = 1; i < qValues.Length; i++)
            {
                if (qValues[i] > qValues[bestIndex]) bestIndex = i;
            }
            return (QAction)bestIndex;
        }

        public void SaveToCsv()
        {
            _storage.Save();
        }

        private void LogStats(string where)
        {
            double sum = 0;
            foreach (var kv in _storage.Data)
                foreach (float v in kv.Value) sum += v;
            UnityEngine.Debug.Log($"[QTable {where}] estados: {_storage.Data.Count} | suma Q: {sum:F2}");
        }

    }
}
