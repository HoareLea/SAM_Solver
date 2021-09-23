using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SAM.Geometry.Solver;
using SAM.Geometry.Planar;
using SAM.Geometry.Spatial;
using SAM.Math;

namespace SAM.Analytical.Solver.Classes
{
    public class SAM_GraphSolver
    {
        public double Tolerance { get; private set; }
        private List<Edge> Edges { get; set; }
        private List<Node> Nodes { get; set; }
        public SAM_GraphSolver(List<Segment2D> lines, List<double> weights, double snapTolerance)
        {
            Tolerance = snapTolerance;
            Edges = new List<Edge>();
            Nodes = new List<Node>();
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
            Node nodeFrom = Nodes.FirstOrDefault(nd => nd.IsCoincident(edge.GetStart(), Tolerance));
            if (nodeFrom == null)
            {
                nodeFrom = new Node(edge.GetStart(), weight);
                Nodes.Add(nodeFrom);
            }
            else
            {
                nodeFrom.MergeIn(edge.GetStart(), weight);
            }
            Node nodeTo = Nodes.FirstOrDefault(nd => nd.IsCoincident(edge.GetEnd(), Tolerance));
            if (nodeTo == null)
            {
                nodeTo = new Node(edge.GetEnd(), weight);
                Nodes.Add(nodeTo);
            }
            else
            {
                nodeTo.MergeIn(edge.GetStart(), weight);
            }
            Edges.Add(new Edge(Edges.Count, nodeFrom, nodeTo));
        }

        private class Node
        {
            public Point2D Location { get; private set; }
            public List<double> Weights { get; private set; }
            public List<Point2D> CoincidentPoints { get; private set; }

            public Node(Point2D pt, double weight)
            {
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
                    if (SAM.Core.Query.AlmostEqual(Weights[i], maxWeight, weightTolerance))
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
                return CoincidentPoints.Any(pt => pt.AlmostEquals(point, tolerance));
                //return CoincidentPoints.Any(pt => (pt.X - point.X) * (pt.X - point.X) +
                //    (pt.Y - point.Y) * (pt.Y - point.Y) <= tolerance2);
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
    }
}
