using SAM.Geometry.Planar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAM.Analytical.Solver.Classes
{
    public class SAM_ExtensionSolver
    {
        public double Tolerance { get; private set; }
        private List<Intersection> _intersections { get; set; }
        private List<Edge> _edges { get; set; }

        public SAM_ExtensionSolver(List<Segment2D> sourceLines, List<double> maxExtensions, double tolerance)
        {
            Tolerance = tolerance;
            _intersections = new List<Intersection>();
            _edges = new List<Edge>();
            for (int i = 0; i < sourceLines.Count; i++)
            {
                AddEdge(i, sourceLines[i], maxExtensions[i]);
            }
        }
        public List<Segment2D> Solve()
        {
            List<Segment2D> extendedLines = new List<Segment2D>();
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
        private void AddEdge(int index, Segment2D line, double maxExtension)
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
            public Point2D Location { get; private set; }
            public HalfEdge[] Participants { get; private set; }
            public Intersection(Point2D intersectionPoint, List<HalfEdge> participants)
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
                    double intersectionParamOnExtension = Participants[i].ExtensionSegment.GetParameter(this.Location);
                    if (Participants[i].IsNaked)
                    {
                        if (intersectionParamOnExtension > Participants[i].OriginalEndOnExtensionParam)
                        { // assign different cost to trimming and extending
                            cost += Participants[i].OriginalEnd.Distance(this.Location);
                        }
                        else
                        {
                            cost += Participants[i].OriginalEnd.Distance(this.Location) / 2; // trimming is twice cheaper
                        }
                    }
                    else
                    { // the only cost is extension beyond its current furthest point
                        Point2D currentEnd = Participants[i].GetFarthestActiveIntersection();
                        double currentEndParam = Participants[i].ExtensionSegment.GetParameter(currentEnd);
                        if (intersectionParamOnExtension > currentEndParam)
                        { // assign different cost to trimming and extending
                            cost += currentEnd.Distance(this.Location);
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
            public Point2D OriginalEnd { get; private set; }
            public double OriginalEndOnExtensionParam { get; private set; }
            public Segment2D ExtensionSegment { get; private set; }
            public Segment2D FullSegment { get; private set; }
            public List<Intersection> Intersections { get; private set; }

            public static void CreateHalves(Edge parent, out HalfEdge startHalf, out HalfEdge endHalf)
            {
                startHalf = new HalfEdge();
                endHalf = new HalfEdge();
                startHalf.OriginalEnd = parent.BaseLine.GetStart();
                endHalf.OriginalEnd = parent.BaseLine.GetEnd();
                startHalf.Parent = parent;
                endHalf.Parent = parent;
                double extensionAsParameter = parent.MaxExtension / parent.BaseLine.GetLength();
                double startT0 = extensionAsParameter <= 0.5 ? extensionAsParameter : 0.5;
                double startT1 = -1 * extensionAsParameter;
                startHalf.ExtensionSegment = new Segment2D(parent.BaseLine.Point2D(startT0), parent.BaseLine.Point2D(startT1));
                startHalf.FullSegment = new Segment2D(parent.BaseLine.Point2D(0.5), parent.BaseLine.Point2D(startT1));
                double endT0 = (1 - extensionAsParameter) >= 0.5 ? (1 - extensionAsParameter) : 0.5;
                double endT1 = 1 + extensionAsParameter;
                endHalf.ExtensionSegment = new Segment2D(parent.BaseLine.Point2D(endT0), parent.BaseLine.Point2D(endT1));
                endHalf.FullSegment = new Segment2D(parent.BaseLine.Point2D(0.5), parent.BaseLine.Point2D(endT1));
                startHalf.OriginalEndOnExtensionParam = startHalf.ExtensionSegment.GetParameter(startHalf.OriginalEnd);
                endHalf.OriginalEndOnExtensionParam = endHalf.ExtensionSegment.GetParameter(endHalf.OriginalEnd);
            }

            private HalfEdge()
            {
                Parent = null;
                IsNaked = true;
                //FullSegment = Line.Unset;
                //ExtensionSegment = Line.Unset;
                FullSegment = new Segment2D(Point2D.Invalid, Point2D.Invalid);
                ExtensionSegment = new Segment2D(Point2D.Invalid, Point2D.Invalid);
                Intersections = new List<Intersection>();
            }

            public Point2D GetFarthestActiveIntersection()
            {
                Point2D farthestPoint = Point2D.Invalid;
                double farthestParam = -1;
                for (int i = 0; i < Intersections.Count; i++)
                {
                    if (!Intersections[i].IsActive)
                    {
                        continue;
                    }
                    double thisParam = this.ExtensionSegment.GetParameter(Intersections[i].Location);
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
                //double thisParam = 0;
                //double otherParam = 0;
                //BoundingBox2D bboxThis = this.FullSegment.GetBoundingBox();
                //BoundingBox2D bboxOther = other.FullSegment.GetBoundingBox();
                x = null;

                intersected = this.FullSegment.Intersect(other.FullSegment, tolerance);
                //intersected = bboxThis.InRange(bboxOther, tolerance);
                //intersected = Rhino.Geometry.Intersect.Intersection.LineLine(this.FullSegment, other.FullSegment, 
                //    out thisParam, out otherParam, tolerance, true);

                if (intersected)
                {
                    //Point3d intersectionPoint = (this.ExtensionSegment.PointAt(thisParam) + other.FullSegment.PointAt(otherParam)) / 2; // average for precision?
                    Point2D intersectionPoint = this.FullSegment.Intersection(other.FullSegment, true, tolerance);
                    //Point3d intersectionPoint = this.FullSegment.PointAt(thisParam);
                    // check if any is a true participant of the intersection
                    List<HalfEdge> trueParticipants = new List<HalfEdge>();
                    bool thisParticipates = false;
                    bool otherParticipates = false;
                    if (this.ExtensionSegment.Distance(intersectionPoint) <= tolerance)
                    {
                        trueParticipants.Add(this);
                        thisParticipates = true;
                    }
                    if (other.ExtensionSegment.Distance(intersectionPoint) <= tolerance)
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
            public Segment2D BaseLine { get; private set; }
            public Segment2D ExtendedLine { get; private set; }
            public double MaxExtension { get; private set; }
            public HalfEdge StartHalf { get; private set; }
            public HalfEdge EndHalf { get; private set; }

            public Edge(int index, Segment2D line, double maxExtension)
            {
                Index = index;
                BaseLine = line;
                Segment2D extended = line;
                extended.Extend(maxExtension, true, true);
                //extended.Extend(maxExtension, maxExtension);
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

            public Segment2D GetResult()
            {
                Point2D newStart = StartHalf.GetFarthestActiveIntersection();
                Point2D newEnd = EndHalf.GetFarthestActiveIntersection();

                if (newStart == Point2D.Invalid)
                {
                    newStart = BaseLine.GetStart();
                }
                if (newEnd == Point2D.Invalid)
                {
                    newEnd = BaseLine.GetEnd();
                }

                return new Segment2D(newStart, newEnd);
            }
        }
    }
}
