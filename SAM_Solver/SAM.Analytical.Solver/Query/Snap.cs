using System.Collections.Generic;
using System.Linq;
using SAM.Geometry.Solver;
using SAM.Geometry.Planar;
using SAM.Geometry.Spatial;
using System;

namespace SAM.Analytical.Solver
{
    public static partial class Query
    {
        public static void Snap(List<Panel> panels, double elevation, Grid2D grid2D, Func<Panel, double> weights, double tolerance = Core.Tolerance.Distance)
        {
            if(panels == null || grid2D == null)
            {
                return;
            }

            //Create section plane (Plane on given elevation)
            Plane plane = Plane.WorldXY.GetMoved(new Vector3D(0, 0, elevation)) as Plane;

            //Get geometry of panels has been created by section. Dictionary alows to receive panel for given geometry.
            //ISegmentable2D - 2D geometry which can be described by connected segments such as Polyline, Polygon (closed polyline), rectangle, triangle, BoundingBox etc.
            Dictionary<Panel, List<ISegmentable2D>> dictionary = panels.SectionDictionary<ISegmentable2D>(plane, tolerance);

            //Temporary list to collect all the Segment2Ds for Panels section
            List<Segment2D> segment2Ds_Temp = new List<Segment2D>();

            //Temporary list to collect all the ISegmentable2Ds for Panels section
            List<ISegmentable2D> segmentable2Ds = new List<ISegmentable2D>();

            foreach (KeyValuePair<Panel, List<ISegmentable2D>> keyValuePair in dictionary)
            {
                //Panel is analytical representation of Wall, Floor, Roof etc.
                Panel panel = keyValuePair.Key;

                //Get weight for the given panel to determine sanp behaviour
                double weight = weights(panel);

                //Panel geometry is always planar! Use Face3D object to retreive panel Geometry. Face3D contains External Edge and Internal Edges
                Face3D face3D = panel.GetFace3D();

                //You can get intersection geometry using PlanarIntersectionResult
                PlanarIntersectionResult planarIntersectionResult = Geometry.Spatial.Create.PlanarIntersectionResult(plane, face3D, Core.Tolerance.Angle, tolerance);

                //This is example how to get intersection 2D geometry from PlanarIntersectionResult
                List<ISegmentable2D> segmentable2Ds_Intersection = planarIntersectionResult.GetGeometry2Ds<ISegmentable2D>();

                //This is example how to get intersection 3D geometry from PlanarIntersectionResult
                List<ISegmentable3D> segmentable3Ds = planarIntersectionResult.GetGeometry3Ds<ISegmentable3D>();

                //Iterating through section geometry for panel
                foreach (ISegmentable2D segmentable2D in keyValuePair.Value)
                {
                    //In the most of the cases section geometry will be single segment.
                    List<Segment2D> segment2Ds = segmentable2D.GetSegments();
                    foreach(Segment2D segment2D in segment2Ds)
                    {
                        segment2Ds_Temp.Add(segment2D);
                    }

                    segmentable2Ds.Add(segmentable2D);
                }

            }

            //Split given list of segment2Ds to include intersection points
            segment2Ds_Temp = segment2Ds_Temp.Split(tolerance);

            //Find all closed loops created by created by given segment2ds
            List<Polygon2D> polygon2Ds = Geometry.Planar.Create.Polygon2Ds(segment2Ds_Temp, tolerance);

            //Gets External Polygon2Ds
            List<Polygon2D> polygon2Ds_External = Geometry.Planar.Query.ExternalPolygon2Ds(polygon2Ds);

            //Convering 2D geometry to 3D geometry on given plane
            List<Polygon3D> polygon3Ds = polygon2Ds.ConvertAll(x => plane.Convert(x));

            //Example of 2D operations on segments:

            Segment2D segment2D_1 = segment2Ds_Temp.First();
            Segment2D segment2D_2 = segment2Ds_Temp.Last();

            //segment direction (unit vector)
            Vector2D vector2D = segment2D_1.Direction;

            //Intersection of two segments. Second parameter determines if segments are bounded. If sets to false intersection point may not lay on given segments
            Point2D point2D_Intersection = segment2D_1.Intersection(segment2D_2, true, tolerance);

            //Closest point on segment to given point2D
            Point2D point2D_Closest = segment2D_1.Closest(segment2D_2[0]);

            //BoundingBox for given segment2Ds:
            BoundingBox2D boundingBox2D_1 = segment2D_1.GetBoundingBox();
            BoundingBox2D boundingBox2D_2 = segment2D_2.GetBoundingBox();

            //Check if boundingBox2D_2 is in range of boundingBox2D_1. This can be used as initial check for intersection when perforamnce is priority
            if (boundingBox2D_1.InRange(boundingBox2D_2, tolerance))
            {
                //Another way to get intersection information for two segments. point2D_Closest_1 is closest point on segment2D_1 to intersection Point2D, point2D_Closest_2 is closest point on segment2D_2 
                Point2D point2D_Intersection_Temp = segment2D_1.Intersection(segment2D_2, out Point2D point2D_Closest_1, out Point2D point2D_Closest2, tolerance);

                //Check if Point2D of segment2D_2 is on segment2D_1
                bool isOn = segment2D_1.On(segment2D_2[0], tolerance);
            }

            //Tracing sample

            //To receive  ray trace data use TraceData Query. Inputs: start point, direction, list of geometry will be check for ray hit. Outputs: tuple with hit point, segment being hit, and hit direction
            List<System.Tuple<Point2D, Segment2D, Vector2D>> traceData = Geometry.Planar.Query.TraceData(segment2D_1[0], segment2D_1.Direction, segmentable2Ds);

            //Fast way to receive first hit
            Vector2D vector2D_RayTrace = Geometry.Planar.Query.TraceFirst(segment2D_1[0], segment2D_1.Direction, segmentable2Ds);

        }
    }
}
