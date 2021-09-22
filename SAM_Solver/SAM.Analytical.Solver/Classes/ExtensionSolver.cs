using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAM.Analytical.Solver.Classes
{
    public class ExtensionSolver
    {
        public double Tolerance { get; private set; }
        private List<Intersection> _intersections { get; set; }
        private List<Edge> _edges { get; set; }

        public ExtensionSolver(List<Line> sourceLines, List<double> maxExtensions, double tolerance)
        {
            Tolerance = tolerance;
            _intersections = new List<Intersection>();
            _edges = new List<Edge>();
            for (int i = 0; i < sourceLines.Count; i++)
            {
                AddEdge(i, sourceLines[i], maxExtensions[i]);
            }
        }
        public List<Line> Solve()
        {
            List<Line> extendedLines = new List<Line>();
            //reset
            for (int i = 0; i < _intersections.Count; i++)
            { // mark all demanded intersections as inactive
                _intersections[i].IsActive = false;
            }
            for (int i = 0; i < _edges.Count; i++)
            { // mark all demanded intersections as inactive
                _edges[i].ResetStatus();
            }
            int safetyCounter = 0;
            List<Intersection> unresolvedIntersections = _intersections.Where(x => x.NakedParticipantsCount() > 0).ToList();
            while (unresolvedIntersections.Count > 0 && safetyCounter < 10000)
            { // while any intersection is not resolved
                safetyCounter++;

                // find the cheapest intersection
                Intersection cheapest = unresolvedIntersections[0];
                double lowestCost = unresolvedIntersections[0].GetCost();
                for (int i = 1; i < unresolvedIntersections.Count; i++)
                {
                    if (lowestCost < Tolerance)
                    {
                        break;
                    }
                    double currentCost = unresolvedIntersections[i].GetCost();
                    if (currentCost < lowestCost)
                    {
                        cheapest = unresolvedIntersections[i];
                        lowestCost = currentCost;
                    }
                }
                // activate the cheapest
                cheapest.Activate();
                // filter
                unresolvedIntersections = unresolvedIntersections.Where(x => x.NakedParticipantsCount() > 0).ToList();
            }
            for (int i = 0; i < _edges.Count; i++)
            {
                extendedLines.Add(_edges[i].GetResult());
            }
            return extendedLines;
        }

        // Adds an Edge and any new resulting intersections
        private void AddEdge(int index, Line line, double maxExtension)
        {
            Edge e = new Edge(index, line, maxExtension);
            for (int i = 0; i < _edges.Count; i++)
            {
                List<Intersection> xList = e.RegisterIntersections(_edges[i], Tolerance);
                if (xList.Count > 0)
                {
                    _intersections.AddRange(xList);
                }
            }
            _edges.Add(e);
        }

        private class Intersection
        {
            public bool IsActive { get; set; }
            public Point3d Location { get; private set; }
            public HalfEdge[] Participants { get; private set; }
            public Intersection(Point3d intersectionPoint, List<HalfEdge> participants)
            {
                IsActive = false;
                Location = intersectionPoint;
                Participants = participants.ToArray();
            }

            public void Activate()
            {
                IsActive = true;
                for (int i = 0; i < Participants.Length; i++)
                {
                    Participants[i].IsNaked = false;
                }
            }

            public double GetCost()
            {
                double cost = 0;
                for (int i = 0; i < Participants.Length; i++)
                {
                    double intersectionParamOnExtension = Participants[i].ExtensionSegment.ClosestParameter(this.Location);
                    if (Participants[i].IsNaked)
                    {
                        if (intersectionParamOnExtension > Participants[i].OriginalEndOnExtensionParam)
                        { // assign different cost to trimming and extending
                            cost += Participants[i].OriginalEnd.DistanceTo(this.Location);
                        }
                        else
                        {
                            cost += Participants[i].OriginalEnd.DistanceTo(this.Location) / 2; // trimming is twice cheaper
                        }
                    }
                    else
                    { // the only cost is extension beyond its current furthest point
                        Point3d currentEnd = Participants[i].GetFarthestActiveIntersection();
                        double currentEndParam = Participants[i].ExtensionSegment.ClosestParameter(currentEnd);
                        if (intersectionParamOnExtension > currentEndParam)
                        { // assign different cost to trimming and extending
                            cost += currentEnd.DistanceTo(this.Location);
                        }
                        else
                        {
                            cost += 0; // no trimming cost
                        }
                    }
                }
                cost /= Participants.Length;
                return cost;
            }

            public int NakedParticipantsCount()
            {
                return Participants.Count(e => e.IsNaked);
            }
        }

        private class HalfEdge
        {
            public Edge Parent { get; private set; }
            public bool IsNaked { get; set; }
            public Point3d OriginalEnd { get; private set; }
            public double OriginalEndOnExtensionParam { get; private set; }
            public Line ExtensionSegment { get; private set; }
            public Line FullSegment { get; private set; }
            public List<Intersection> Intersections { get; private set; }

            public static void CreateHalves(Edge parent, out HalfEdge startHalf, out HalfEdge endHalf)
            {
                startHalf = new HalfEdge();
                endHalf = new HalfEdge();
                startHalf.OriginalEnd = parent.BaseLine.From;
                endHalf.OriginalEnd = parent.BaseLine.To;
                startHalf.Parent = parent;
                endHalf.Parent = parent;
                double extensionAsParameter = parent.MaxExtension / parent.BaseLine.Length;
                double startT0 = extensionAsParameter <= 0.5 ? extensionAsParameter : 0.5;
                double startT1 = -1 * extensionAsParameter;
                startHalf.ExtensionSegment = new Line(parent.BaseLine.PointAt(startT0), parent.BaseLine.PointAt(startT1));
                startHalf.FullSegment = new Line(parent.BaseLine.PointAt(0.5), parent.BaseLine.PointAt(startT1));
                double endT0 = (1 - extensionAsParameter) >= 0.5 ? (1 - extensionAsParameter) : 0.5;
                double endT1 = 1 + extensionAsParameter;
                endHalf.ExtensionSegment = new Line(parent.BaseLine.PointAt(endT0), parent.BaseLine.PointAt(endT1));
                endHalf.FullSegment = new Line(parent.BaseLine.PointAt(0.5), parent.BaseLine.PointAt(endT1));
                startHalf.OriginalEndOnExtensionParam = startHalf.ExtensionSegment.ClosestParameter(startHalf.OriginalEnd);
                endHalf.OriginalEndOnExtensionParam = endHalf.ExtensionSegment.ClosestParameter(endHalf.OriginalEnd);
            }

            private HalfEdge()
            {
                Parent = null;
                IsNaked = true;
                FullSegment = Line.Unset;
                ExtensionSegment = Line.Unset;
                Intersections = new List<Intersection>();
            }

            public Point3d GetFarthestActiveIntersection()
            {
                Point3d farthestPoint = Point3d.Unset;
                double farthestParam = -1;
                for (int i = 0; i < Intersections.Count; i++)
                {
                    if (!Intersections[i].IsActive)
                    {
                        continue;
                    }
                    double thisParam = this.ExtensionSegment.ClosestParameter(Intersections[i].Location);
                    if (thisParam > farthestParam)
                    {
                        farthestParam = thisParam;
                        // it's important to get the intersection point rather than recalculate it from the parameter (numerical tolerance issues)
                        farthestPoint = Intersections[i].Location;
                    }
                }
                return farthestPoint;
            }

            public bool TryRegisterIntersections(HalfEdge other, double tolerance, out Intersection x)
            {
                bool intersected = false;
                double thisParam = 0;
                double otherParam = 0;
                x = null;
                intersected = Rhino.Geometry.Intersect.Intersection.LineLine(this.FullSegment, other.FullSegment, out thisParam, out otherParam, tolerance, true);
                if (intersected)
                {
                    //Point3d intersectionPoint = (this.ExtensionSegment.PointAt(thisParam) + other.FullSegment.PointAt(otherParam)) / 2; // average for precision?
                    Point3d intersectionPoint = this.FullSegment.PointAt(thisParam);
                    // check if any is a true participant of the intersection
                    List<HalfEdge> trueParticipants = new List<HalfEdge>();
                    bool thisParticipates = false;
                    bool otherParticipates = false;
                    if (this.ExtensionSegment.DistanceTo(intersectionPoint, true) <= tolerance)
                    {
                        trueParticipants.Add(this);
                        thisParticipates = true;
                    }
                    if (other.ExtensionSegment.DistanceTo(intersectionPoint, true) <= tolerance)
                    {
                        trueParticipants.Add(other);
                        otherParticipates = true;
                    }
                    if (trueParticipants.Count == 0)
                    {
                        return false;
                    }
                    x = new Intersection(intersectionPoint, trueParticipants);
                    if (thisParticipates)
                    {
                        this.Intersections.Add(x);
                    }
                    if (otherParticipates)
                    {
                        other.Intersections.Add(x);
                    }
                }
                return intersected;
            }
        }

        private class Edge
        {
            public int Index { get; private set; }
            public Line BaseLine { get; private set; }
            public Line ExtendedLine { get; private set; }
            public double MaxExtension { get; private set; }
            public HalfEdge StartHalf { get; private set; }
            public HalfEdge EndHalf { get; private set; }

            public Edge(int index, Line line, double maxExtension)
            {
                Index = index;
                BaseLine = line;
                Line extended = line;
                extended.Extend(maxExtension, maxExtension);
                ExtendedLine = extended;
                MaxExtension = maxExtension;
                HalfEdge start = null;
                HalfEdge end = null;
                HalfEdge.CreateHalves(this, out start, out end);
                StartHalf = start;
                EndHalf = end;
            }

            public List<Intersection> RegisterIntersections(Edge other, double tolerance)
            {
                List<Intersection> intersections = new List<Intersection>();
                Intersection SxS = null;
                Intersection SxE = null;
                Intersection ExS = null;
                Intersection ExE = null;
                if (this.StartHalf.TryRegisterIntersections(other.StartHalf, tolerance, out SxS))
                {
                    intersections.Add(SxS);
                }
                if (this.StartHalf.TryRegisterIntersections(other.EndHalf, tolerance, out SxE))
                {
                    intersections.Add(SxE);
                }
                if (this.EndHalf.TryRegisterIntersections(other.StartHalf, tolerance, out ExS))
                {
                    intersections.Add(ExS);
                }
                if (this.EndHalf.TryRegisterIntersections(other.EndHalf, tolerance, out ExE))
                {
                    intersections.Add(ExE);
                }

                return intersections;
            }

            public void ResetStatus()
            {
                StartHalf.IsNaked = true;
                EndHalf.IsNaked = true;
            }

            public Line GetResult()
            {
                Point3d newStart = StartHalf.GetFarthestActiveIntersection();
                Point3d newEnd = EndHalf.GetFarthestActiveIntersection();

                if (newStart == Point3d.Unset)
                {
                    newStart = BaseLine.From;
                }
                if (newEnd == Point3d.Unset)
                {
                    newEnd = BaseLine.To;
                }

                return new Line(newStart, newEnd);
            }
        }
    }
}
