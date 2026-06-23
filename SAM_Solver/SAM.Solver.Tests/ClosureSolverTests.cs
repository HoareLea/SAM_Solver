// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using SAM.Geometry.Planar;
using SAM.Geometry.Solver;
using Xunit;

namespace SAM.Solver.Tests
{
    /// <summary>
    /// Regression tests for <see cref="ClosureSolver"/>, the consolidated NTS snap-round -> node ->
    /// polygonise pipeline that recognises closed rooms and dangling (naked) edges from 2D wall axes.
    /// </summary>
    public class ClosureSolverTests
    {
        private static Segment2D Segment(double x1, double y1, double x2, double y2)
        {
            return new Segment2D(new Point2D(x1, y1), new Point2D(x2, y2));
        }

        private static List<Segment2D> Square(double side)
        {
            return new List<Segment2D>
            {
                Segment(0, 0, side, 0),
                Segment(side, 0, side, side),
                Segment(side, side, 0, side),
                Segment(0, side, 0, 0)
            };
        }

        [Fact]
        public void ClosedSquare_IsOneRoom_WithNoDangles()
        {
            ClosureSolver.Result result = ClosureSolver.Polygonize(Square(1.0));

            Assert.Single(result.Rooms);
            Assert.Equal(0, result.DangleCount);
        }

        [Fact]
        public void SquareWithProtrudingTail_StaysOneRoom_AndReportsTheTailAsADangle()
        {
            List<Segment2D> segments = Square(1.0);
            segments.Add(Segment(0, 0, -0.5, 0)); // tail off a corner: not part of any loop

            ClosureSolver.Result result = ClosureSolver.Polygonize(segments);

            Assert.Single(result.Rooms);
            Assert.True(result.DangleCount >= 1);
        }

        [Fact]
        public void SliverLoop_BelowMinSide_IsNotCountedAsARoom()
        {
            // A 0.1 m square: a real closed loop, but below the default 0.2 m sliver threshold.
            ClosureSolver.Result result = ClosureSolver.Polygonize(Square(0.1));

            Assert.Empty(result.Rooms);
        }

        [Fact]
        public void FewerThanThreeSegments_EncloseNoRoom_ButStillReportNakedEnds()
        {
            // Two open segments can't enclose a region, but their free ends are real naked ends and must
            // be reported as dangles - otherwise a small open level reads as fully closed and the
            // auto-tune loop never targets its gap.
            List<Segment2D> segments = new List<Segment2D> { Segment(0, 0, 1, 0), Segment(1, 0, 1, 1) };

            ClosureSolver.Result result = ClosureSolver.Polygonize(segments);

            Assert.Empty(result.Rooms);
            Assert.True(result.DangleCount >= 1);
        }

        [Fact]
        public void NullInput_ReturnsEmptyResult_WithoutThrowing()
        {
            ClosureSolver.Result result = ClosureSolver.Polygonize(null);

            Assert.Empty(result.Rooms);
            Assert.Empty(result.DangleEnds);
        }

        [Fact]
        public void NodeSegments_CrossingSegments_EachSplitIntoTwo()
        {
            // A horizontal and a vertical segment crossing at the origin: each must be noded into two.
            List<Segment2D> segments = new List<Segment2D>
            {
                Segment(-1, 0, 1, 0),
                Segment(0, -1, 0, 1)
            };

            List<List<Segment2D>> noded = ClosureSolver.NodeSegments(segments);

            Assert.Equal(2, noded[0].Count);
            Assert.Equal(2, noded[1].Count);
        }

        [Fact]
        public void NodeSegments_TJunction_SplitsOnlyTheThroughSegment()
        {
            // B touches A at A's interior but at B's own endpoint: A splits in two, B stays whole.
            List<Segment2D> segments = new List<Segment2D>
            {
                Segment(0, 0, 2, 0), // A: through segment
                Segment(1, 0, 1, 1)  // B: stem meeting A at (1,0)
            };

            List<List<Segment2D>> noded = ClosureSolver.NodeSegments(segments);

            Assert.Equal(2, noded[0].Count);
            Assert.Single(noded[1]);
        }

        [Fact]
        public void NodeSegments_NonTouchingSegments_StayWhole()
        {
            List<Segment2D> segments = new List<Segment2D>
            {
                Segment(0, 0, 1, 0),
                Segment(0, 5, 1, 5)
            };

            List<List<Segment2D>> noded = ClosureSolver.NodeSegments(segments);

            Assert.Single(noded[0]);
            Assert.Single(noded[1]);
        }

        [Fact]
        public void NodeSegments_EmptyInput_ReturnsEmpty()
        {
            Assert.Empty(ClosureSolver.NodeSegments(new List<Segment2D>()));
            Assert.Empty(ClosureSolver.NodeSegments(null));
        }
    }
}
