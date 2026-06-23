// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using System.Linq;
using SAM.Core;
using SAM.Geometry.Spatial;
using SAM.Geometry.Solver;
using Xunit;

namespace SAM.Solver.Tests
{
    /// <summary>
    /// End-to-end smoke tests for the single-pass <see cref="SnapSolver"/>: a clean, already-closed
    /// room solves into wall faces with no naked ends.
    /// </summary>
    public class SnapSolverTests
    {
        // A vertical wall panel (axis x1,y1 -> x2,y2 extruded from zMin to zMax).
        private static Face3D Wall(double x1, double y1, double x2, double y2, double zMin, double zMax)
        {
            List<Point3D> points = new List<Point3D>
            {
                new Point3D(x1, y1, zMin),
                new Point3D(x2, y2, zMin),
                new Point3D(x2, y2, zMax),
                new Point3D(x1, y1, zMax)
            };
            return new Face3D(new Polygon3D(points));
        }

        // Four walls forming a closed 4 m x 4 m room, 3 m tall.
        private static List<Face3D> SquareRoom()
        {
            return new List<Face3D>
            {
                Wall(0, 0, 4, 0, 0, 3),
                Wall(4, 0, 4, 4, 0, 3),
                Wall(4, 4, 0, 4, 0, 3),
                Wall(0, 4, 0, 0, 0, 3)
            };
        }

        private static void Solve(List<Face3D> panels, out int faceCount, out int nakedCount)
        {
            SnapSolver solver = new SnapSolver(
                panels,
                new List<double>(),
                new List<double>(),
                new List<double>(),
                new List<Range<double>> { new Range<double>(0.0, 3.0) });
            solver.Execute();
            faceCount = solver.SnappedWalls.Sum(level => level.Count);
            nakedCount = solver.NakedEnds.Sum(level => level.Count);
        }

        [Fact]
        public void CleanClosedRoom_SolvesIntoWallFacesWithNoNakedEnds()
        {
            Solve(SquareRoom(), out int faceCount, out int nakedCount);

            Assert.True(faceCount > 0);   // the model actually solved into wall faces
            Assert.Equal(0, nakedCount);  // a closed room leaves no open ends
        }
    }
}
