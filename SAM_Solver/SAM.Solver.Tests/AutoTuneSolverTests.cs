// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using SAM.Analytical;
using SAM.Analytical.Solver;
using SAM.Core;
using SAM.Geometry.Spatial;
using Xunit;

namespace SAM.Solver.Tests
{
    /// <summary>
    /// Real-Panel integration tests for <see cref="AutoTuneSolver{T}"/>: it solves a clean closed room,
    /// its escalation never regresses closure, and its room output matches the report's loop count.
    /// </summary>
    public class AutoTuneSolverTests
    {
        private static readonly Construction WallConstruction = new Construction("Wall");

        // A vertical wall Panel (axis x1,y1 -> x2,y2 extruded from zMin to zMax).
        private static Panel Wall(double x1, double y1, double x2, double y2, double zMin, double zMax)
        {
            List<Point3D> points = new List<Point3D>
            {
                new Point3D(x1, y1, zMin),
                new Point3D(x2, y2, zMin),
                new Point3D(x2, y2, zMax),
                new Point3D(x1, y1, zMax)
            };
            return Analytical.Create.Panel(WallConstruction, PanelType.Wall, new Face3D(new Polygon3D(points)));
        }

        // Four walls forming a closed 4 m x 4 m room, 3 m tall.
        private static List<Panel> SquareRoom()
        {
            return new List<Panel>
            {
                Wall(0, 0, 4, 0, 0, 3),
                Wall(4, 0, 4, 4, 0, 3),
                Wall(4, 4, 0, 4, 0, 3),
                Wall(0, 4, 0, 0, 0, 3)
            };
        }

        private static AutoTuneSolver<Panel> Solver(List<Panel> panels, bool escalate)
        {
            return new AutoTuneSolver<Panel>(panels, new List<Range<double>> { new Range<double>(0.0, 3.0) })
            {
                EscalateMaxExtend = escalate,
                MaxRounds = 6
            };
        }

        [Fact]
        public void CleanRoom_BaselineSolvesIntoOneClosedRoomWithNoNakedEnds()
        {
            AutoTuneSolver<Panel> solver = Solver(SquareRoom(), escalate: false);

            List<Panel> solved = solver.Execute(out List<Point3D> naked, out AutoTuneReport report);

            Assert.NotNull(solved);
            Assert.Equal(1, report.FinalClosedLoopCount);
            Assert.Equal(0, report.FinalNakedCount);
            Assert.Empty(naked ?? new List<Point3D>());
        }

        [Fact]
        public void RoomsOutput_MatchesReportClosedLoopCount()
        {
            AutoTuneSolver<Panel> solver = Solver(SquareRoom(), escalate: true);

            solver.Execute(out List<Point3D> _, out AutoTuneReport report);

            Assert.Equal(report.FinalClosedLoopCount, solver.Rooms.Count);
        }

        // A 4 m x 4 m room split by a redundant "double wall" - two parallel dividers a 0.1 m slit
        // apart at x=2.0 and x=2.1 - giving two real rooms either side of an excluded sliver.
        private static List<Panel> DoubleWallRoom()
        {
            return new List<Panel>
            {
                Wall(0, 0, 4, 0, 0, 3),
                Wall(4, 0, 4, 4, 0, 3),
                Wall(4, 4, 0, 4, 0, 3),
                Wall(0, 4, 0, 0, 0, 3),
                Wall(2.0, 0, 2.0, 4, 0, 3),
                Wall(2.1, 0, 2.1, 4, 0, 3)
            };
        }

        [Theory]
        [InlineData(0.2)]
        [InlineData(0.5)]
        public void SlitMerge_NeverErasesARoomOrAddsNakedEnds(double mergeSlitWidth)
        {
            // The slit merge is closure-guarded: a group whose merge would drop the room count or
            // enclosed area is left unmerged. So enabling it can never lose a room versus merge-off,
            // nor open a new naked end - whatever the chosen width.
            AutoTuneReport Solve(double width)
            {
                AutoTuneSolver<Panel> solver = new AutoTuneSolver<Panel>(
                    DoubleWallRoom(), new List<Range<double>> { new Range<double>(0.0, 3.0) })
                {
                    EscalateMaxExtend = false,
                    MergeSlitWidth = width
                };
                solver.Execute(out List<Point3D> _, out AutoTuneReport report);
                return report;
            }

            AutoTuneReport off = Solve(0.0);
            AutoTuneReport merged = Solve(mergeSlitWidth);

            Assert.Equal(2, off.FinalClosedLoopCount);                              // two real rooms, 0.1 m sliver excluded
            Assert.True(merged.FinalClosedLoopCount >= off.FinalClosedLoopCount);   // merge never erases a room
            Assert.True(merged.FinalNakedCount <= off.FinalNakedCount);            // ...nor opens a new naked end
        }

        [Fact]
        public void Escalation_NeverRegressesClosureVersusBaseline()
        {
            // The acceptance rule rejects any escalation round that drops the room count, so the tuned
            // result can only hold or improve on the baseline - never lose rooms or gain naked ends.
            AutoTuneSolver<Panel> solver = Solver(SquareRoom(), escalate: true);

            solver.Execute(out List<Point3D> _, out AutoTuneReport report);

            Assert.True(report.FinalClosedLoopCount >= report.BaselineClosedLoopCount);
            Assert.True(report.FinalNakedCount <= report.BaselineNakedCount);
        }

        [Fact]
        public void Rollback_RestoresAZeroMaxExtendBaseline()
        {
            // A caller can seed MaxExtend = 0 to stop a wall extending. A rejected escalation round must
            // restore that zero baseline, not leave the escalated value the failed round wrote.
            Panel panel = Wall(0, 0, 4, 0, 0, 3);
            panel.SetValue(SolverParameter.MaxExtend, 0.0);

            AutoTuneSolver<Panel> solver = new AutoTuneSolver<Panel>(
                new List<Panel> { panel }, new List<Range<double>> { new Range<double>(0.0, 3.0) });

            Dictionary<Panel, double> baselineBucket = new Dictionary<Panel, double> { { panel, 0.3 } };
            Dictionary<Panel, double> baselineExtend = new Dictionary<Panel, double> { { panel, 0.0 } };
            Dictionary<Panel, int> escalationStep = new Dictionary<Panel, int> { { panel, -1 } };

            solver.ApplyStep(panel, 0, baselineBucket, baselineExtend, escalationStep); // escalate
            Assert.True(panel.TryGetValue(SolverParameter.MaxExtend, out double escalated) && escalated > 0);

            solver.ApplyStep(panel, -1, baselineBucket, baselineExtend, escalationStep); // roll back
            Assert.True(panel.TryGetValue(SolverParameter.MaxExtend, out double restored));
            Assert.Equal(0.0, restored, 6);
        }
    }
}
