using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using System.Diagnostics;

namespace QMind
{
    public class QTableStorage
    {
        private readonly string _filePath;
        private readonly string[] _actionNames;

        public Dictionary<string, float[]> Data { get; } = new();

        public QTableStorage(string fileName = "TablaQ.csv")
        {
            _actionNames = Enum.GetNames(typeof(QAction));
            
            string grupo = ResolveGrupoNameFromCallStack();
            if (string.IsNullOrWhiteSpace(grupo))
                throw new Exception("No se pudo detectar el Grupo desde el call stack. Asegúrate de que el código del alumno está en namespace GrupoA/GrupoB/etc.");

            // Convención: carpeta = namespace
            // Assets/Scripts/GrupoX/TablaQ.csv
            string absoluteFolder = Path.Combine(Application.dataPath, "Scripts", grupo);

            if (!Directory.Exists(absoluteFolder))
                throw new DirectoryNotFoundException($"No existe la carpeta esperada: {absoluteFolder}");

            _filePath = Path.Combine(absoluteFolder, fileName);
            UnityEngine.Debug.Log($"[QTableStorage] Q-table path: {_filePath}");
            Console.WriteLine($"[QTableStorage] Q-table path: {_filePath}");
            
            Load();
        }

        private static string ResolveGrupoNameFromCallStack()
        {
            var st = new StackTrace(false);

            for (int i = 0; i < st.FrameCount; i++)
            {
                var t = st.GetFrame(i)?.GetMethod()?.DeclaringType;
                var ns = t?.Namespace;
                if (string.IsNullOrWhiteSpace(ns)) continue;

                if (ns.StartsWith("Grupo", StringComparison.Ordinal))
                    return ns.Split('.')[0]; // "GrupoA"
            }

            return null;
        }

        public void Save()
        {
            using var writer = new StreamWriter(_filePath, false, Encoding.UTF8);

            // Cabecera
            writer.Write("State");
            foreach (var actionName in _actionNames)
            {
                writer.Write($";{actionName}");
            }
            writer.WriteLine();

            // Filas
            foreach (var kv in Data)
            {
                string stateKey = kv.Key;
                float[] qValues = kv.Value;

                writer.Write(stateKey);
                for (int i = 0; i < _actionNames.Length; i++)
                {
                    writer.Write(";");
                    writer.Write(qValues[i].ToString(CultureInfo.InvariantCulture));
                }

                writer.WriteLine();
            }
        }

        private void Load()
        {
            if (!File.Exists(_filePath))
            {
                UnityEngine.Debug.Log($"[QTableStorage] No Q-table file found at {_filePath}, starting empty.");
                return;
            }

            using var reader = new StreamReader(_filePath, Encoding.UTF8);

            // Leemos cabecera
            var headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                UnityEngine.Debug.LogWarning("[QTableStorage] Empty Q-table file, starting with no data.");
                return;
            }

            // Leemos datos
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(';');
                if (parts.Length < 2)
                    continue;

                string stateKey = parts[0];
                var qValues = new float[_actionNames.Length];

                for (int i = 0; i < _actionNames.Length; i++)
                {
                    int csvIndex = i + 1;
                    if (csvIndex < parts.Length &&
                        float.TryParse(parts[csvIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    {
                        qValues[i] = value;
                    }
                    else
                    {
                        qValues[i] = 0f;
                    }
                }

                Data[stateKey] = qValues;
            }

            UnityEngine.Debug.Log($"[QTableStorage] Q-table loaded from {_filePath} with {Data.Count} states.");
        }       
    }
}