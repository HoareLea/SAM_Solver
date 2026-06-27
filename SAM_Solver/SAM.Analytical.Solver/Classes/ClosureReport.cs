// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Core;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.Planar;
using SAM.Geometry.Solver;
using SAM.Geometry.Spatial;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAM.Analytical.Solver
{
    /// <summary>
    /// A per-level closure summary of a solved model, scored with
    /// <see cref="SAM.Geometry.Solver.ClosureSolver"/>: recognised rooms (sliver-filtered), their total
    /// enclosed area, naked ends (dangling edges) and the wall-segment count. Shared by the Solver and
    /// AutoTuneSolver components so both report on the same metric and can be compared directly.
    /// </summary>
    public class ClosureReport
    {
        /// <summary>Closure figures for a single elevation level.</summary>
        public class Level
        {
            public double Elevation { get; set; }
            public int Rooms { get; set; }
            public double RoomArea { get; set; }
            public int NakedEnds { get; set; }
            public int WallSegments { get; set; }

            public override string ToString()
            {
                return Elevation.ToString("0.###") + " m: rooms=" + Rooms
                    + ", area=" + RoomArea.ToString("0.##") + " m2, naked=" + NakedEnds
                    + ", segments=" + WallSegments;
            }
        }

        public List<Level> Levels { get; } = new List<Level>();

        public int TotalRooms => Levels.Sum(x => x.Rooms);
        public double TotalRoomArea => Levels.Sum(x => x.RoomArea);
        public int TotalNakedEnds => Levels.Sum(x => x.NakedEnds);
        public int TotalWallSegments => Levels.Sum(x => x.WallSegments);
        public int LevelCount => Levels.Count;

        /// <summary>
        /// Section <paramref name="solved"/> at each level (range.Min + <paramref name="offset"/>) and
        /// score the resulting wall axes with <see cref="ClosureSolver"/>. Best-effort per level: a level
        /// that cannot be sectioned/polygonised contributes a zero row rather than failing.
        /// </summary>
        public static ClosureReport Create(IEnumerable<IFace3DObject> solved, IEnumerable<Range<double>> ranges, double offset, double toleranceAngle, double toleranceDistance)
        {
            ClosureReport report = new ClosureReport();
            if (solved == null || ranges == null)
            {
                return report;
            }

            List<IFace3DObject> solved_Temp = solved.Where(x => x != null).ToList();

            foreach (Range<double> range in ranges)
            {
                if (range == null)
                {
                    continue;
                }

                Level level = new Level { Elevation = range.Min };
                report.Levels.Add(level);

                try
                {
                    Plane plane = SAM.Geometry.Spatial.Create.Plane(range.Min + offset);

                    Dictionary<IFace3DObject, List<ISegmentable2D>> dictionary = solved_Temp.SectionDictionary<IFace3DObject, ISegmentable2D>(plane, toleranceAngle, toleranceDistance);

                    List<Segment2D> segment2Ds = new List<Segment2D>();
                    if (dictionary != null)
                    {
                        foreach (List<ISegmentable2D> segmentable2Ds in dictionary.Values)
                        {
                            if (segmentable2Ds == null)
                            {
                                continue;
                            }

                            foreach (ISegmentable2D segmentable2D in segmentable2Ds)
                            {
                                List<Segment2D> segments = segmentable2D?.GetSegments();
                                if (segments != null)
                                {
                                    segment2Ds.AddRange(segments);
                                }
                            }
                        }
                    }

                    level.WallSegments = segment2Ds.Count;

                    ClosureSolver.Result closure = ClosureSolver.Polygonize(segment2Ds);
                    level.Rooms = closure.RoomCount;
                    level.NakedEnds = closure.DangleCount;
                    level.RoomArea = closure.Rooms.Sum(x => System.Math.Abs(x.GetArea()));
                }
                catch
                {
                    // Best-effort: leave this level's row at zero.
                }
            }

            return report;
        }

        /// <summary>
        /// Compact one-line totals, e.g. "rooms=12, area=486.5 m2, naked ends=3, levels=4".
        /// </summary>
        public string Summary()
        {
            return "rooms=" + TotalRooms + ", area=" + TotalRoomArea.ToString("0.##") + " m2"
                + ", naked ends=" + TotalNakedEnds + ", levels=" + LevelCount
                + ", wall segments=" + TotalWallSegments;
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Closure report — " + Summary());
            stringBuilder.AppendLine("per level (rooms / area m2 / naked ends / wall segments):");
            foreach (Level level in Levels)
            {
                stringBuilder.AppendLine("  " + level.ToString());
            }
            return stringBuilder.ToString().TrimEnd();
        }
    }
}
