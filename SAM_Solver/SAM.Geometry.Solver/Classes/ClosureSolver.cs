// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Geometry.Planar;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using NetTopologySuite.Noding;
using NetTopologySuite.Noding.Snapround;
using NetTopologySuite.Operation.Polygonize;

namespace SAM.Geometry.Solver
{
    /// <summary>
    /// Single home for the NetTopologySuite snap-round -> node -> polygonise pipeline that recognises
    /// closed rooms and dangling ("naked") edges from a set of 2D wall axes.
    ///
    /// This raw-NTS logic previously lived inline in <c>AutoTuneSolver.PolygonizeLevel</c>; it is
    /// consolidated here so closure scoring and room extraction share one implementation and one set of
    /// thresholds. (<see cref="SnapSolver.ComputeClosure"/> intentionally keeps its separate,
    /// <c>Create.Polygon2Ds</c>-based path: it drives the closure-guarded parallel slit-merge and is
    /// covered by ClosureTests, so it is deliberately not re-pointed at this pipeline.)
    /// </summary>
    public static class ClosureSolver
    {
        /// <summary>
        /// Snap-rounding grid (m) used when polygonising. Small enough to keep geometry faithful (so real
        /// ~0.1 m gaps stay open as dangles), large enough to node coincident endpoints robustly.
        /// </summary>
        public const double DefaultGridSize = 0.01;

        /// <summary>
        /// A polygonised loop counts as a real room only if the smaller side of its bounding box is at
        /// least this (m); smaller loops are slivers. Mirrors SnapSolver's sliver filter.
        /// </summary>
        public const double DefaultSliverMinSide = 0.2;

        /// <summary>
        /// Outcome of polygonising one set of wall axes: the recognised rooms (sliver-filtered), the
        /// endpoints of the dangling edges (the naked ends, for gap targeting), and the dangle count.
        /// </summary>
        public class Result
        {
            public List<Polygon2D> Rooms { get; } = new List<Polygon2D>();
            public List<Point2D> DangleEnds { get; } = new List<Point2D>();
            public int DangleCount { get; set; }
            public int RoomCount => Rooms.Count;
        }

        /// <summary>
        /// Snap-round, node and polygonise a set of 2D wall axes. Loops whose bounding-box minimum side is
        /// below <paramref name="sliverMinSide"/> are discarded as slivers. Never throws on degenerate
        /// input - it returns an empty result instead.
        /// </summary>
        public static Result Polygonize(IEnumerable<Segment2D> segment2Ds, double gridSize = DefaultGridSize, double sliverMinSide = DefaultSliverMinSide)
        {
            Result result = new Result();
            if (segment2Ds == null)
            {
                return result;
            }

            List<Segment2D> segments = new List<Segment2D>();
            foreach (Segment2D segment in segment2Ds)
            {
                if (segment != null)
                {
                    segments.Add(segment);
                }
            }

            if (segments.Count == 0)
            {
                return result;
            }
            // Note: we do NOT bail out below 3 segments. A region needs 3+ axes to enclose, but one or two
            // open axes still have naked endpoints, and the polygonizer reports them as dangles. Returning
            // early here would zero the dangle count on small open levels, so the auto-tune loop would
            // never target their gaps and the closure report would undercount naked ends.

            PrecisionModel precisionModel = new PrecisionModel(1.0 / gridSize);
            SnapRoundingNoder noder = new SnapRoundingNoder(precisionModel);

            List<ISegmentString> segmentStrings = new List<ISegmentString>();
            foreach (Segment2D segment in segments)
            {
                segmentStrings.Add(new NodedSegmentString(
                    new Coordinate[] { new Coordinate(segment.Start.X, segment.Start.Y), new Coordinate(segment.End.X, segment.End.Y) },
                    null));
            }

            noder.ComputeNodes(segmentStrings);

            GeometryFactory geometryFactory = new GeometryFactory(precisionModel);
            List<NetTopologySuite.Geometries.Geometry> lines = new List<NetTopologySuite.Geometries.Geometry>();
            foreach (ISegmentString segmentString in noder.GetNodedSubstrings())
            {
                Coordinate[] coordinates = segmentString.Coordinates;
                if (coordinates.Length >= 2)
                {
                    lines.Add(geometryFactory.CreateLineString(coordinates));
                }
            }

            Polygonizer polygonizer = new Polygonizer();
            polygonizer.Add(lines);

            foreach (NetTopologySuite.Geometries.Geometry geometry in polygonizer.GetPolygons())
            {
                Envelope envelope = geometry.EnvelopeInternal;
                if (System.Math.Min(envelope.Width, envelope.Height) < sliverMinSide)
                {
                    continue; // sliver loop: not a real room
                }

                Polygon2D polygon2D = ToPolygon2D(geometry);
                if (polygon2D != null)
                {
                    result.Rooms.Add(polygon2D);
                }
            }

            // Naked ends = nodes of degree 1 in the noded line network. Polygonizer.GetDangles() returns
            // whole dangling lines, so its endpoints include the junction where a tail meets a closed loop
            // (degree >= 2) - counting that would inflate the naked-end count and make AutoTune escalate
            // panels around closed-room corners. Tally the degree of every noded endpoint and keep only
            // the free (degree-1) ones.
            Dictionary<string, int> endpointDegree = new Dictionary<string, int>();
            Dictionary<string, Coordinate> endpointCoordinate = new Dictionary<string, Coordinate>();
            foreach (ISegmentString segmentString in noder.GetNodedSubstrings())
            {
                Coordinate[] coordinates = segmentString.Coordinates;
                if (coordinates == null || coordinates.Length < 2)
                {
                    continue;
                }

                foreach (Coordinate coordinate in new[] { coordinates[0], coordinates[coordinates.Length - 1] })
                {
                    string key = System.Math.Round(coordinate.X / gridSize) + "_" + System.Math.Round(coordinate.Y / gridSize);
                    endpointDegree[key] = endpointDegree.TryGetValue(key, out int degree) ? degree + 1 : 1;
                    endpointCoordinate[key] = coordinate;
                }
            }

            foreach (KeyValuePair<string, int> entry in endpointDegree)
            {
                if (entry.Value != 1)
                {
                    continue; // connected node, not a naked end
                }

                result.DangleCount++;
                Coordinate coordinate = endpointCoordinate[entry.Key];
                result.DangleEnds.Add(new Point2D(coordinate.X, coordinate.Y));
            }

            return result;
        }

        /// <summary>
        /// Robustly node a set of 2D segments against each other with NetTopologySuite snap-rounding:
        /// the whole set is split at every true intersection on a single precision grid in one pass.
        /// Returns, per input segment (by index), the sub-segments it was noded into — so a segment with
        /// no intersection yields one piece, and a segment crossed once yields two. Null input segments
        /// yield an empty list at their index, preserving index alignment. Never throws on degenerate
        /// input.
        /// </summary>
        public static List<List<Segment2D>> NodeSegments(IReadOnlyList<Segment2D> segment2Ds, double gridSize = DefaultGridSize)
        {
            int count = segment2Ds == null ? 0 : segment2Ds.Count;

            List<List<Segment2D>> result = new List<List<Segment2D>>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(new List<Segment2D>());
            }

            if (count == 0)
            {
                return result;
            }

            PrecisionModel precisionModel = new PrecisionModel(1.0 / gridSize);
            SnapRoundingNoder noder = new SnapRoundingNoder(precisionModel);

            List<ISegmentString> segmentStrings = new List<ISegmentString>();
            for (int i = 0; i < count; i++)
            {
                Segment2D segment = segment2Ds[i];
                if (segment == null)
                {
                    continue;
                }

                // Attach the source index as context so the noded substrings can be mapped back.
                segmentStrings.Add(new NodedSegmentString(
                    new Coordinate[] { new Coordinate(segment.Start.X, segment.Start.Y), new Coordinate(segment.End.X, segment.End.Y) },
                    i));
            }

            if (segmentStrings.Count == 0)
            {
                return result;
            }

            noder.ComputeNodes(segmentStrings);

            foreach (ISegmentString segmentString in noder.GetNodedSubstrings())
            {
                if (!(segmentString.Context is int index) || index < 0 || index >= count)
                {
                    continue;
                }

                Coordinate[] coordinates = segmentString.Coordinates;
                for (int k = 0; k < coordinates.Length - 1; k++)
                {
                    Point2D start = new Point2D(coordinates[k].X, coordinates[k].Y);
                    Point2D end = new Point2D(coordinates[k + 1].X, coordinates[k + 1].Y);
                    if (start.Distance(end) > 0)
                    {
                        result[index].Add(new Segment2D(start, end));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Convert an NTS polygon's exterior ring to a SAM <see cref="Polygon2D"/>, dropping the ring's
        /// closing duplicate vertex (Polygon2D is implicitly closed).
        /// </summary>
        private static Polygon2D ToPolygon2D(NetTopologySuite.Geometries.Geometry geometry)
        {
            NetTopologySuite.Geometries.Polygon polygon = geometry as NetTopologySuite.Geometries.Polygon;
            Coordinate[] coordinates = polygon != null ? polygon.ExteriorRing.Coordinates : geometry?.Coordinates;
            if (coordinates == null || coordinates.Length < 4)
            {
                return null;
            }

            List<Point2D> points = new List<Point2D>();
            for (int i = 0; i < coordinates.Length - 1; i++)
            {
                points.Add(new Point2D(coordinates[i].X, coordinates[i].Y));
            }

            if (points.Count < 3)
            {
                return null;
            }

            return new Polygon2D(points);
        }
    }
}
