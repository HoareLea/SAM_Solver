// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using SAM.Geometry.Planar;
using SAM.Geometry.Solver;
using Xunit;

namespace SAM.Solver.Tests
{
    /// <summary>
    /// Regression tests for <see cref="GraphSolver"/>, which de-duplicates wall axes by merging
    /// coincident endpoints into shared nodes. These also exercise the cell-grid node lookup.
    /// </summary>
    public class GraphSolverTests
    {
        private const double Tolerance = 1e-3;

        private static Segment2D Segment(double x1, double y1, double x2, double y2)
        {
            return new Segment2D(new Point2D(x1, y1), new Point2D(x2, y2));
        }

        [Fact]
        public void ExactDuplicateSegments_CollapseToSingleEdge_WithBothSources()
        {
            List<Segment2D> lines = new List<Segment2D> { Segment(0, 0, 1, 0), Segment(0, 0, 1, 0) };
            GraphSolver solver = new GraphSolver(lines, new List<double> { 1.0, 1.0 }, Tolerance);

            List<List<int>> sources;
            List<Segment2D> unique = solver.Solve(out sources);

            Assert.Single(unique);
            Assert.Equal(2, sources[0].Count);
        }

        [Fact]
        public void NearCoincidentEndpointsWithinTolerance_StillCollapse()
        {
            // Endpoints differ by ~7e-7, well inside Tolerance: the grid must find the existing node
            // in a neighbouring cell, otherwise the two edges would not share nodes and stay separate.
            List<Segment2D> lines = new List<Segment2D>
            {
                Segment(0, 0, 1, 0),
                Segment(0.0000005, 0.0000005, 1.0000005, 0)
            };
            GraphSolver solver = new GraphSolver(lines, new List<double> { 1.0, 1.0 }, Tolerance);

            List<List<int>> sources;
            List<Segment2D> unique = solver.Solve(out sources);

            Assert.Single(unique);
            Assert.Equal(2, sources[0].Count);
        }

        [Fact]
        public void DistinctFarApartSegments_StaySeparate()
        {
            List<Segment2D> lines = new List<Segment2D> { Segment(0, 0, 1, 0), Segment(0, 5, 1, 5) };
            GraphSolver solver = new GraphSolver(lines, new List<double> { 1.0, 1.0 }, Tolerance);

            List<List<int>> sources;
            List<Segment2D> unique = solver.Solve(out sources);

            Assert.Equal(2, unique.Count);
        }

        [Fact]
        public void DegenerateSubToleranceEdge_IsDropped()
        {
            // The second segment's endpoints collapse into one node, leaving a zero-length edge
            // that Solve must discard.
            List<Segment2D> lines = new List<Segment2D>
            {
                Segment(0, 0, 1, 0),
                Segment(5, 5, 5.0000001, 5)
            };
            GraphSolver solver = new GraphSolver(lines, new List<double> { 1.0, 1.0 }, Tolerance);

            List<List<int>> sources;
            List<Segment2D> unique = solver.Solve(out sources);

            Assert.Single(unique);
        }
    }
}
