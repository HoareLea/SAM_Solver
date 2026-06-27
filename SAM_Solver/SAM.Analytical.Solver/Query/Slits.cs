// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using SAM.Core;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.Planar;
using SAM.Geometry.Solver;
using SAM.Geometry.Spatial;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Solver
{
    // Deliberately NOT named "Query": an SAM.Analytical.Solver.Query would shadow the inherited
    // SAM.Analytical.Query at unqualified "Query." call sites in this project. Callers use the
    // Slits(...) extension method, so the containing class name is irrelevant to them.
    public static partial class SolverQuery
    {
        /// <summary>
        /// Remaining internal "double-wall"/slit diagnostics in a set of face objects: pairs of
        /// near-parallel, overlapping wall axes (sectioned per level) separated by a small perpendicular
        /// gap. Each returned <see cref="Segment3D"/> is a short connector spanning that gap; the source
        /// objects whose section axes formed those pairs are returned via <paramref name="slitPanels"/>.
        /// Pure diagnostic - nothing is modified. Shared by <see cref="AutoTuneSolver{T}"/> and reused by
        /// downstream tooling (e.g. the OCCT Clean3D/Solve3D components) so the detection has one home.
        /// </summary>
        /// <param name="face3DObjects">Objects to section and inspect (panels, partitions, ...).</param>
        /// <param name="slitPanels">Out: source objects whose section axes touch a remaining slit (deduplicated).</param>
        /// <param name="ranges">Level elevation ranges; null derives them from the geometry.</param>
        /// <param name="offset">Section-plane offset above each level's base (m).</param>
        /// <param name="minGap">Minimum perpendicular gap reported as a slit (m).</param>
        /// <param name="maxGap">Maximum perpendicular gap reported as a slit (m).</param>
        /// <param name="maxOverlap">Maximum parallel overlap length reported (m); 0 = no limit.</param>
        /// <param name="toleranceAngle">Angle tolerance for sectioning (radians).</param>
        /// <param name="toleranceDistance">Distance tolerance (m).</param>
        public static List<Segment3D> Slits<T>(
            this IEnumerable<T> face3DObjects,
            out List<T> slitPanels,
            IEnumerable<Range<double>> ranges = null,
            double offset = 0.2,
            double minGap = 0.02,
            double maxGap = 0.5,
            double maxOverlap = 2.0,
            double toleranceAngle = Tolerance.Angle,
            double toleranceDistance = Tolerance.Distance) where T : IParameterizedSAMObject, IFace3DObject
        {
            slitPanels = new List<T>();

            List<Segment3D> result = new List<Segment3D>();
            List<T> solved = face3DObjects?.Where(x => x != null).ToList();
            if (solved == null || solved.Count < 2)
            {
                return result;
            }

            List<Range<double>> ranges_Temp = ranges?.ToList();
            if (ranges_Temp == null || ranges_Temp.Count == 0)
            {
                ranges_Temp = solved.ElevationRanges(toleranceDistance);
            }

            if (ranges_Temp == null || ranges_Temp.Count == 0)
            {
                return result;
            }

            // A slit pair is within maxGap perpendicular distance, so an STRtree of segment boxes grown by
            // that gap returns a strict superset of the pairs an all-pairs scan would test.
            const double MinSlitOverlap = 0.05;
            const double ParallelDot = 0.99;
            double minSlitGap = System.Math.Max(0, minGap);
            double maxSlitGap = maxGap > minSlitGap ? maxGap : 0.5;

            List<T> panels = new List<T>();
            foreach (Range<double> range in ranges_Temp)
            {
                try
                {
                    List<LevelSegment<T>> segments = LevelSegmentSources(solved, range, offset, toleranceAngle, toleranceDistance, out Plane plane);
                    if (segments.Count < 2)
                    {
                        continue;
                    }

                    double elevation = plane?.Origin?.Z ?? range.Min;

                    STRtree<int> index = new STRtree<int>();
                    for (int i = 0; i < segments.Count; i++)
                    {
                        index.Insert(SegmentEnvelope(segments[i].Segment, 0), i);
                    }

                    for (int i = 0; i < segments.Count; i++)
                    {
                        Vector2D ui = segments[i].Segment.Direction.Unit;
                        foreach (int j in index.Query(SegmentEnvelope(segments[i].Segment, maxSlitGap)))
                        {
                            if (j <= i)
                            {
                                continue; // count each pair once
                            }

                            Vector2D uj = segments[j].Segment.Direction.Unit;
                            if (System.Math.Abs((ui.X * uj.X) + (ui.Y * uj.Y)) < ParallelDot)
                            {
                                continue;
                            }

                            if (TryCreateSlitMarker(segments[i].Segment, segments[j].Segment, elevation, minSlitGap, maxSlitGap, MinSlitOverlap, maxOverlap, out Segment3D slit))
                            {
                                result.Add(slit);
                                AddPanel(panels, segments[i].Source);
                                AddPanel(panels, segments[j].Source);
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            slitPanels = panels;
            return result;
        }

        private static void AddPanel<T>(List<T> panels, T panel)
        {
            if (panel == null)
            {
                return;
            }

            if (!panels.Any(x => object.ReferenceEquals(x, panel)))
            {
                panels.Add(panel);
            }
        }

        /// <summary>Section <paramref name="solved"/> at the level's section plane and collect the resulting
        /// 2D wall-axis segments tagged with the source object they came from.</summary>
        private static List<LevelSegment<T>> LevelSegmentSources<T>(List<T> solved, Range<double> range, double offset, double toleranceAngle, double toleranceDistance, out Plane plane) where T : IParameterizedSAMObject, IFace3DObject
        {
            plane = SAM.Geometry.Spatial.Create.Plane(range.Min + offset);

            List<LevelSegment<T>> result = new List<LevelSegment<T>>();
            Dictionary<T, List<ISegmentable2D>> dictionary = solved.SectionDictionary<T, ISegmentable2D>(plane, toleranceAngle, toleranceDistance);
            if (dictionary == null)
            {
                return result;
            }

            foreach (KeyValuePair<T, List<ISegmentable2D>> keyValuePair in dictionary)
            {
                List<ISegmentable2D> segmentable2Ds = keyValuePair.Value;
                if (segmentable2Ds == null)
                {
                    continue;
                }

                foreach (ISegmentable2D segmentable2D in segmentable2Ds)
                {
                    List<Segment2D> segments = segmentable2D?.GetSegments();
                    if (segments == null)
                    {
                        continue;
                    }

                    foreach (Segment2D segment in segments)
                    {
                        result.Add(new LevelSegment<T>(segment, keyValuePair.Key));
                    }
                }
            }

            return result;
        }

        private static bool TryCreateSlitMarker(Segment2D reference, Segment2D other, double elevation, double minGap, double maxGap, double minOverlap, double maxOverlap, out Segment3D slit)
        {
            slit = null;

            double startParameter = reference.ClosestParameter(other.Start);
            double endParameter = reference.ClosestParameter(other.End);
            double overlapMin = System.Math.Max(0, System.Math.Min(startParameter, endParameter));
            double overlapMax = System.Math.Min(1, System.Math.Max(startParameter, endParameter));
            if (overlapMax <= overlapMin)
            {
                return false;
            }

            double overlapLength = (overlapMax - overlapMin) * reference.GetLength();
            if (overlapLength < minOverlap)
            {
                return false;
            }

            if (maxOverlap > 0 && overlapLength > maxOverlap)
            {
                return false;
            }

            double parameter = (overlapMin + overlapMax) / 2;
            Point2D referencePoint = reference.GetPoint(parameter);
            double otherParameter = other.ClosestParameter(referencePoint).Clamp(0, 1);
            Point2D otherPoint = other.GetPoint(otherParameter);

            double gap = referencePoint.Distance(otherPoint);
            if (gap <= minGap || gap > maxGap)
            {
                return false;
            }

            slit = new Segment3D(
                new Point3D(referencePoint.X, referencePoint.Y, elevation),
                new Point3D(otherPoint.X, otherPoint.Y, elevation));
            return true;
        }

        /// <summary>Axis-aligned bounding box of a segment, optionally grown by <paramref name="expansion"/>.</summary>
        private static Envelope SegmentEnvelope(Segment2D segment, double expansion)
        {
            Point2D start = segment.Start;
            Point2D end = segment.End;
            Envelope envelope = new Envelope(
                System.Math.Min(start.X, end.X), System.Math.Max(start.X, end.X),
                System.Math.Min(start.Y, end.Y), System.Math.Max(start.Y, end.Y));
            if (expansion > 0)
            {
                envelope.ExpandBy(expansion);
            }
            return envelope;
        }

        private struct LevelSegment<T>
        {
            public Segment2D Segment;
            public T Source;

            public LevelSegment(Segment2D segment, T source)
            {
                Segment = segment;
                Source = source;
            }
        }
    }
}
