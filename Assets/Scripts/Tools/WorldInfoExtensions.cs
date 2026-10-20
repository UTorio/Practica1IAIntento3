#region Copyright
// MIT License
// 
// Copyright (c) 2023 David María Arribas
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
#endregion

using NavigationDJIA.World;
using UnityEngine;

namespace Tools
{
    /// <summary>
    ///     Distance helpers for <see cref="WorldInfo" />.
    ///     NavigationDJIA.World.CellInfo.ManhattanDistance is private inside Navigation.dll,
    ///     so CellInfo.Distance(other, CellInfo.DistanceType.Manhattan) is the only public way
    ///     to reach it. These wrappers expose it without duplicating the formula.
    /// </summary>
    public static class WorldInfoExtensions
    {
        /// <summary>
        ///     Manhattan distance in cells between two cells of the world.
        /// </summary>
        /// <param name="worldInfo">World used as call site. The metric is purely geometric, so it is not read.</param>
        /// <param name="from">First cell</param>
        /// <param name="to">Second cell</param>
        /// <returns>|from.x - to.x| + |from.y - to.y|</returns>
        public static int ManhattanDistance(this WorldInfo worldInfo, CellInfo from, CellInfo to)
        {
            return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
        }

        /// <summary>
        ///     Manhattan distance in cells between two cells of the world, as a float.
        ///     Useful when a reward function expects a continuous value.
        /// </summary>
        public static float ManhattanDistanceF(this WorldInfo worldInfo, CellInfo from, CellInfo to)
        {
            return worldInfo.ManhattanDistance(from, to);
        }

        /// <summary>
        ///     Manhattan distance in cells between two unity world positions, resolved against this world.
        /// </summary>
        public static int ManhattanDistance(this WorldInfo worldInfo, Vector3 from, Vector3 to)
        {
            return worldInfo.ManhattanDistance(worldInfo.FromVector3(from), worldInfo.FromVector3(to));
        }

        /// <summary>
        ///     Distance between two cells using the NavigationDJIA metric (Manhattan or Euclidean).
        /// </summary>
        public static float Distance(this WorldInfo worldInfo, CellInfo from, CellInfo to, CellInfo.DistanceType distanceType)
        {
            return from.Distance(to, distanceType);
        }

        /// <summary>
        ///     Manhattan distance between a cell and the closest of the given cells.
        ///     Returns int.MaxValue if targets is null or empty.
        /// </summary>
        /// <param name="worldInfo">World used as call site. The metric is purely geometric, so it is not read.</param>
        /// <param name="from">Origin cell</param>
        /// <param name="targets">Candidate cells, typically the ones still holding a treasure</param>
        public static int ManhattanDistanceToClosest(this WorldInfo worldInfo, CellInfo from, CellInfo[] targets)
        {
            if (targets == null || targets.Length == 0)
            {
                return int.MaxValue;
            }

            int minimum = int.MaxValue;

            foreach (CellInfo target in targets)
            {
                minimum = Mathf.Min(minimum, worldInfo.ManhattanDistance(from, target));
            }

            return minimum;
        }

        /// <summary>
        ///     Manhattan distance between a cell and the world exit, or int.MaxValue if the world has no exit.
        /// </summary>
        public static int ManhattanDistanceToExit(this WorldInfo worldInfo, CellInfo from)
        {
            CellInfo exit = worldInfo.Exit;

            return exit == null ? int.MaxValue : worldInfo.ManhattanDistance(from, exit);
        }
    }
}