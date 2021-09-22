using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAM.Analytical.Grasshopper.Solver.Classes
{
    public class GraphSolver
    {
        public double Tolerance { get; private set; }
        private List<Edge> Edges { get; set; }
        private List<Node> Nodes { get; set; }
        public GraphSolver(List<Line> lines, List<double> weights, double snapTolerance)
        {
            Tolerance = snapTolerance;
            Edges = new List<Edge>();
            Nodes = new List<Node>();
            for (int i = 0; i < lines.Count; i++)
            {
                AddEdge(lines[i], weights[i]); // this is unsafe of course, to assume exact numbers of items (but safe in the context of the snap solver
            }
        }
        public List<Line> Solve(out List<List<int>> sourceIndices)
        {
            List<Line> uniqueEdges = new List<Line>();
            sourceIndices = new List<List<int>>();
            bool[] processed = new bool[Edges.Count];
            for (int i = 0; i < Edges.Count; i++)
            {
                if (processed[i])
                {
                    continue;
                }
                Line thisEdge = Edges[i].GetLine();
                if (thisEdge.Length < Tolerance)
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

        private void AddEdge(Line edge, double weight)
        {
            Node nodeFrom = Nodes.FirstOrDefault(nd => nd.IsCoincident(edge.From, Tolerance));
            if (nodeFrom == null)
            {
                nodeFrom = new Node(edge.From, weight);
                Nodes.Add(nodeFrom);
            }
            else
            {
                nodeFrom.MergeIn(edge.From, weight);
            }
            Node nodeTo = Nodes.FirstOrDefault(nd => nd.IsCoincident(edge.To, Tolerance));
            if (nodeTo == null)
            {
                nodeTo = new Node(edge.To, weight);
                Nodes.Add(nodeTo);
            }
            else
            {
                nodeTo.MergeIn(edge.To, weight);
            }
            Edges.Add(new Edge(Edges.Count, nodeFrom, nodeTo));
        }

        private class Node
        {
            public Point3d Location { get; private set; }
            public List<double> Weights { get; private set; }
            public List<Point3d> CoincidentPoints { get; private set; }

            public Node(Point3d pt, double weight)
            {
                Location = pt;
                Weights = new List<double>() { weight };
                CoincidentPoints = new List<Point3d>() { pt };
            }

            private Point3d CalculateLocation(double weightTolerance = 0.000001)
            {
                double maxWeight = Weights.Max();
                Point3d pointSum = new Point3d(0, 0, 0);
                int count = 0;
                for (int i = 0; i < Weights.Count; i++)
                {
                    if (RhinoMath.EpsilonEquals(Weights[i], maxWeight, weightTolerance))
                    {
                        pointSum += CoincidentPoints[i];
                        count++;
                    }
                }
                return pointSum / count;
            }

            public bool IsCoincident(Point3d point, double tolerance)
            {
                double tolerance2 = tolerance * tolerance;
                return CoincidentPoints.Any(pt => pt.DistanceToSquared(point) <= tolerance2);
            }

            public void MergeIn(Point3d point, double weight)
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
                return From.Location.DistanceTo(To.Location);
            }
            public Line GetLine()
            {
                return new Line(From.Location, To.Location);
            }
            public bool Equals(Edge other)
            {
                return (this.From == other.From && this.To == other.To) || 
                    (this.From == other.To && this.To == other.From);
            }
        }
    }
}
