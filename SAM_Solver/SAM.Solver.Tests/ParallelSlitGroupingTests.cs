// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Geometry.Planar;
using SAM.Geometry.Solver;
using Xunit;

namespace SAM.Solver.Tests
{
    /// <summary>
    /// Unit tests for <see cref="SnapSolver.AreParallelWithinTolerance"/>, the predicate that decides
    /// whether two wall axes form a mergeable "double-wall" slit. It must group genuine slits (parallel,
    /// real axial overlap, small perpendicular gap) while refusing staggered end-to-end pairs that would
    /// otherwise be bridged across a doorway-sized gap.
    /// </summary>
    public class ParallelSlitGroupingTests
    {
        private const double Tolerance = 0.2;

        private static Segment2D Segment(double x1, double y1, double x2, double y2)
        {
            return new Segment2D(new Point2D(x1, y1), new Point2D(x2, y2));
        }

        [Fact]
        public void FullyOverlappingParallelAxes_WithinGap_AreGrouped()
        {
            Segment2D a = Segment(0, 0, 4, 0);
            Segment2D b = Segment(0, 0.1, 4, 0.1);

            Assert.True(SnapSolver.AreParallelWithinTolerance(a, b, Tolerance));
        }

        [Fact]
        public void PartiallyOverlappingParallelAxes_AreGrouped()
        {
            // Overlap only over x in [3, 4]; both midpoints sit past the other segment, so a midpoint-only
            // test would miss this slit (the original blind spot).
            Segment2D a = Segment(0, 0, 4, 0);
            Segment2D b = Segment(3, 0.1, 7, 0.1);

            Assert.True(SnapSolver.AreParallelWithinTolerance(a, b, Tolerance));
        }

        [Fact]
        public void StaggeredParallelAxes_TouchingAtEndpoints_AreNotGrouped()
        {
            // End-to-end with no axial overlap: a minimum-distance-only test would wrongly group these
            // (endpoints are 0.1 m apart) and bridge them into one long wall across the gap.
            Segment2D a = Segment(0, 0, 4, 0);
            Segment2D b = Segment(4, 0.1, 8, 0.1);

            Assert.False(SnapSolver.AreParallelWithinTolerance(a, b, Tolerance));
        }

        [Fact]
        public void ParallelAxes_BeyondPerpendicularGap_AreNotGrouped()
        {
            Segment2D a = Segment(0, 0, 4, 0);
            Segment2D b = Segment(0, 0.5, 4, 0.5);

            Assert.False(SnapSolver.AreParallelWithinTolerance(a, b, Tolerance));
        }

        [Fact]
        public void SkewedAxes_WithinGapAtOneEndButSplayingApart_AreNotGrouped()
        {
            // Within the 0.99 direction tolerance but skewed: gap is 0.05 m at x=0 and ~0.45 m at x=4.
            // A single-sample (b.Start) test would accept it; measuring across the overlap rejects it.
            Segment2D a = Segment(0, 0, 4, 0);
            Segment2D b = Segment(0, 0.05, 4, 0.45);

            Assert.False(SnapSolver.AreParallelWithinTolerance(a, b, Tolerance));
        }

        [Fact]
        public void PerpendicularAxes_AreNotGrouped()
        {
            Segment2D a = Segment(0, 0, 4, 0);
            Segment2D b = Segment(0, 0, 0, 4);

            Assert.False(SnapSolver.AreParallelWithinTolerance(a, b, Tolerance));
        }

        [Fact]
        public void OpenEndpointCount_ClosedLoopHasNoOpenEnds()
        {
            System.Collections.Generic.List<Segment2D> closed = new System.Collections.Generic.List<Segment2D>
            {
                Segment(0, 0, 4, 0),
                Segment(4, 0, 4, 4),
                Segment(4, 4, 0, 4),
                Segment(0, 4, 0, 0)
            };

            Assert.Equal(0, SnapSolver.OpenEndpointCount(closed, 1e-3));
        }

        [Fact]
        public void OpenEndpointCount_CountsTheTwoFreeEndsOfAnOpenRun()
        {
            // Three sides of a square: the two ends of the gap are open, the shared corners are not.
            System.Collections.Generic.List<Segment2D> open = new System.Collections.Generic.List<Segment2D>
            {
                Segment(0, 0, 4, 0),
                Segment(4, 0, 4, 4),
                Segment(4, 4, 0, 4)
            };

            Assert.Equal(2, SnapSolver.OpenEndpointCount(open, 1e-3));
        }
    }
}
