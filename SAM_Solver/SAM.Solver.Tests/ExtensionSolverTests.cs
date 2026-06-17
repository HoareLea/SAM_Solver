// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using SAM.Geometry.Planar;
using SAM.Geometry.Solver;
using Xunit;

namespace SAM.Solver.Tests
{
    /// <summary>
    /// Regression tests for <see cref="ExtensionSolver"/>, which extends/trims wall axes to resolve
    /// intersections. The solver must always return one result segment per input, in order.
    /// </summary>
    public class ExtensionSolverTests
    {
        private const double Tolerance = 1e-6;

        private static Segment2D Segment(double x1, double y1, double x2, double y2)
        {
            return new Segment2D(new Point2D(x1, y1), new Point2D(x2, y2));
        }

        [Fact]
        public void Solve_ReturnsOneValidSegmentPerInput()
        {
            // s2 crosses s1 at (0.5, 0); both should resolve to finite, non-degenerate segments.
            List<Segment2D> lines = new List<Segment2D>
            {
                Segment(0, 0, 1, 0),
                Segment(0.5, -0.5, 0.5, 0.5)
            };
            ExtensionSolver solver = new ExtensionSolver(lines, new List<double> { 0.5, 0.5 }, Tolerance);

            List<Segment2D> result = solver.Solve();

            Assert.Equal(lines.Count, result.Count);
            foreach (Segment2D segment in result)
            {
                Assert.False(segment.Start.IsNaN());
                Assert.False(segment.End.IsNaN());
                Assert.True(segment.GetLength() > 0);
            }
        }

        [Fact]
        public void Solve_WithSingleSegment_ReturnsItUnchangedInCount()
        {
            List<Segment2D> lines = new List<Segment2D> { Segment(0, 0, 2, 0) };
            ExtensionSolver solver = new ExtensionSolver(lines, new List<double> { 0.5 }, Tolerance);

            List<Segment2D> result = solver.Solve();

            Assert.Single(result);
            Assert.False(result[0].Start.IsNaN());
            Assert.False(result[0].End.IsNaN());
        }
    }
}
