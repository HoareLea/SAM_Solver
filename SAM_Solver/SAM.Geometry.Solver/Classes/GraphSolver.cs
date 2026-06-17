// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using System.Linq;
using SAM.Geometry.Planar;
using SAM.Geometry.Spatial;

namespace SAM.Geometry.Solver
{
    public class GraphSolver
    {
        public double Tolerance { get; private set; }
        private List<Edge> Edges { get; set; }
        private List<Node> Nodes { get; set; }
        private NodeGrid _nodeGrid;
        public GraphSolver(List<Segment2D> lines, List<double> weights, double snapTolerance)
        {
            Tolerance = snapTolerance;
            Edges = new List<Edge>();
            Nodes = new List<Node>();
            _nodeGrid = new NodeGrid(snapTolerance);
            for (int i = 0; i < lines.Count; i++)
            {
                AddEdge(lines[i], weights[i]); // this is unsafe of course, to assume exact numbers of items (but safe in the context of the snap solver
            }
        }
        public List<Segment2D> Solve(out List<List<int>> sourceIndices)
        {
            List<Segment2D> uniqueEdges = new List<Segment2D>();
            sourceIndices = new List<List<int>>();
            bool[] processed = new bool[Edges.Count];
            for (int i = 0; i < Edges.Count; i++)
            {
                if (processed[i])
                {
                    continue;
                }

                Segment2D thisEdge = Edges[i].GetLine();
                if (thisEdge.GetLength() < Tolerance)
                {
                    continue;
                }
                List<int> source = new List<int>() { Edges[i].Index };
                for (int j = i + 1; j < Edges.Count; j++)
                {
                    if (Edges[j].Equals(Edges[i]))
                    {
                        source.Add(Edges[j].Index);
                        processed[j] = true;
                    }
                }
                uniqueEdges.Add(thisEdge);
                sourceIndices.Add(source);
            }
            return uniqueEdges;
        }

        private void AddEdge(Segment2D edge, double weight)
        {
            Node nodeFrom = FindCoincidentNode(edge.Start);
            if (nodeFrom == null)
            {
                nodeFrom = new Node(Nodes.Count, edge.Start, weight);
                Nodes.Add(nodeFrom);
            }
            else
            {
                nodeFrom.MergeIn(edge.Start, weight);
            }
            _nodeGrid.Add(edge.Start, nodeFrom.Index);

            Node nodeTo = FindCoincidentNode(edge.End);
            if (nodeTo == null)
            {
                nodeTo = new Node(Nodes.Count, edge.End, weight);
                Nodes.Add(nodeTo);
            }
            else
            {
                nodeTo.MergeIn(edge.End, weight);
            }
            _nodeGrid.Add(edge.End, nodeTo.Index);

            Edges.Add(new Edge(Edges.Count, nodeFrom, nodeTo));
        }

        /// <summary>
        /// Returns the lowest-index existing node coincident with <paramref name="point"/> within
        /// Tolerance, or null. The cell grid narrows the search to the point's neighbourhood while
        /// preserving the original "first coincident node" result of the former linear scan.
        /// </summary>
        private Node FindCoincidentNode(Point2D point)
        {
            int best = -1;
            foreach (int index in _nodeGrid.CandidateNodeIndices(point))
            {
                if ((best == -1 || index < best) && Nodes[index].IsCoincident(point, Tolerance))
                {
                    best = index;
                }
            }
            return best == -1 ? null : Nodes[best];
        }

        private class Node
        {
            public int Index { get; private set; }
            public Point2D Location { get; private set; }
            public List<double> Weights { get; private set; }
            public List<Point2D> CoincidentPoints { get; private set; }

            public Node(int index, Point2D pt, double weight)
            {
                Index = index;
                Location = pt;
                Weights = new List<double>() { weight };
                CoincidentPoints = new List<Point2D>() { pt };
            }

            private Point2D CalculateLocation(double weightTolerance = 0.000001)
            {
                double maxWeight = Weights.Max();
                Point2D pointSum = new Point2D(0, 0);
                int count = 0;
                for (int i = 0; i < Weights.Count; i++)
                {
                    if (Core.Query.AlmostEqual(Weights[i], maxWeight, weightTolerance))
                    {
                        //pointSum += CoincidentPoints[i];
                        pointSum = new Point2D(pointSum.X + CoincidentPoints[i].X,
                            pointSum.Y + CoincidentPoints[i].Y);
                        count++;
                    }
                }
                //return pointSum / count;
                return new Point2D(pointSum.X / count, pointSum.Y / count);
            }

            public bool IsCoincident(Point2D point, double tolerance)
            {
                double tolerance2 = tolerance * tolerance;
                //return CoincidentPoints.Any(pt => pt.DistanceToSquared(point) <= tolerance2);
                //return CoincidentPoints.Any(pt => pt.AlmostEquals(point, tolerance)); // this doesn't work like distance to squared...
                return CoincidentPoints.Any(pt => (pt.X - point.X) * (pt.X - point.X) +
                    (pt.Y - point.Y) * (pt.Y - point.Y) <= tolerance2);
            }

            public void MergeIn(Point2D point, double weight)
            {
                Weights.Add(weight);
                CoincidentPoints.Add(point);
                Location = CalculateLocation();
            }
        }

        private class Edge
        {
            public Node From { get; private set; }
            public Node To { get; private set; }
            public int Index { get; private set; }
            public Edge(int index, Node from, Node to)
            {
                Index = index;
                From = from;
                To = to;
            }
            public double GetLength()
            {
                return From.Location.Distance(To.Location);
            }
            public Segment2D GetLine()
            {
                return new Segment2D(From.Location, To.Location);
            }
            public bool Equals(Edge other)
            {
                return (this.From == other.From && this.To == other.To) ||
                    (this.From == other.To && this.To == other.From);
            }
        }

        /// <summary>
        /// Uniform spatial hash mapping a 2D cell to the node indices registered in it. The cell size is
        /// at least the snap tolerance, so any point within tolerance of a stored node falls in that
        /// node's cell or an immediate neighbour, making a 3x3 neighbourhood lookup exhaustive.
        /// </summary>
        private class NodeGrid
        {
            private readonly double _cellSize;
            private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();

            public NodeGrid(double tolerance)
            {
                // Floor the cell size to avoid integer overflow of cell coordinates when tolerance is tiny;
                // a larger cell only widens the candidate set, never changing the coincidence result.
                _cellSize = System.Math.Max(tolerance, 1e-3);
            }

            private int CellCoordinate(double value)
            {
                return (int)System.Math.Floor(value / _cellSize);
            }

            private static long Key(int cellX, int cellY)
            {
                return ((long)cellX << 32) | (uint)cellY;
            }

            public void Add(Point2D point, int nodeIndex)
            {
                long key = Key(CellCoordinate(point.X), CellCoordinate(point.Y));
                List<int> bucket;
                if (!_cells.TryGetValue(key, out bucket))
                {
                    bucket = new List<int>();
                    _cells[key] = bucket;
                }
                if (!bucket.Contains(nodeIndex))
                {
                    bucket.Add(nodeIndex);
                }
            }

            public IEnumerable<int> CandidateNodeIndices(Point2D point)
            {
                int cellX = CellCoordinate(point.X);
                int cellY = CellCoordinate(point.Y);
                HashSet<int> candidates = new HashSet<int>();
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        List<int> bucket;
                        if (_cells.TryGetValue(Key(cellX + dx, cellY + dy), out bucket))
                        {
                            for (int k = 0; k < bucket.Count; k++)
                            {
                                candidates.Add(bucket[k]);
                            }
                        }
                    }
                }
                return candidates;
            }
        }
    }
}
