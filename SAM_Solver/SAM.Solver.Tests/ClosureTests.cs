// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using SAM.Core;
using SAM.Geometry.Planar;
using SAM.Geometry.Spatial;
using SAM.Geometry.Solver;
using Xunit;

namespace SAM.Solver.Tests
{
    /// <summary>
    /// Tests for the shared closure scorer (<see cref="SnapSolver.ComputeClosure"/>): closed-loop
    /// recognition with sliver loops filtered out. Used by the parallel slit-merge guard.
    /// </summary>
    public class ClosureTests
    {
        private const double Tolerance = 1e-3;

        private static Segment2D Segment(double x1, double y1, double x2, double y2)
        {
            return new Segment2D(new Point2D(x1, y1), new Point2D(x2, y2));
        }

        private static List<Segment2D> Box(double width, double height)
        {
            return new List<Segment2D>
            {
                Segment(0, 0, width, 0),
                Segment(width, 0, width, height),
                Segment(width, height, 0, height),
                Segment(0, height, 0, 0)
            };
        }

        [Fact]
        public void ClosedUnitSquare_IsOneLoopOfAreaOne()
        {
            SnapSolver.ClosureSignature signature = SnapSolver.ComputeClosure(Box(1.0, 1.0), Tolerance);

            Assert.Equal(1, signature.LoopCount);
            Assert.Equal(1.0, signature.Area, 3);
        }

        [Fact]
        public void OpenThreeSidedLoop_IsNotClosed()
        {
            List<Segment2D> open = new List<Segment2D>
            {
                Segment(0, 0, 1, 0),
                Segment(1, 0, 1, 1),
                Segment(1, 1, 0, 1)
                // closing side intentionally omitted
            };

            SnapSolver.ClosureSignature signature = SnapSolver.ComputeClosure(open, Tolerance);

            Assert.Equal(0, signature.LoopCount);
        }

        [Fact]
        public void TinySquareBelowMinRectangleSide_IsIgnored()
        {
            // 0.1 m x 0.1 m closed loop: a real closed loop, but a sliver - both sides below 0.2 m.
            SnapSolver.ClosureSignature signature = SnapSolver.ComputeClosure(Box(0.1, 0.1), Tolerance);

            Assert.Equal(0, signature.LoopCount);
        }

        [Fact]
        public void ThinSlotWithOneSideBelowMin_IsIgnored()
        {
            // 0.1 m x 5 m: large area, but the short side (0.1 m) is below the 0.2 m minimum rectangle side.
            SnapSolver.ClosureSignature signature = SnapSolver.ComputeClosure(Box(0.1, 5.0), Tolerance);

            Assert.Equal(0, signature.LoopCount);
        }
    }
}
