using Rhino.Geometry;

using SAM.Geometry.Spatial;

using System;
using System.Collections.Generic;

namespace SAM.Analytical.Rhino.Solver
{
    public static partial class Query
    {
        /// <summary>
        /// Gets mesh visualization of max snap range.
        /// </summary>
        /// <param name="panels"></param>
        /// <param name="values"></param>
        /// <param name="offset"></param>
        /// <param name="halfResolution"> Half the number of arc vertices.</param>
        /// <returns></returns>
        public static List<Tuple<Mesh, Mesh>> SnapRangeMeshes(this IEnumerable<Panel> panels, out List<double> values, double offset = 0.15, int halfResolution = 30)
        {
            // the range is equal to MaxExtend parameter
            values = null;
            if (panels == null) {
                return null;
            }

            //double oneSideAngle = SnappedWall.OpenNodeSnapAngleRangeRad; // how to get the static parameter?
            double oneSideAngle = 2 * Math.PI / 3; // hardcoded here - but shouldn't be
            double singleStepAngle = oneSideAngle / halfResolution;

            List <Tuple<Mesh, Mesh>> result = new List<Tuple<Mesh, Mesh>>();
            values = new List<double>();
            foreach (Panel panel in panels) {
                if (panel == null) {
                    result.Add(null);
                    continue;
                }

                if (!panel.TryGetValue(Analytical.Solver.SolverParameter.MaxExtend, out double maxExtend)) {
                    result.Add(null);
                    continue;
                }

                Face3D face3D = panel.GetFace3D();
                if (face3D == null) {
                    result.Add(null);
                    continue;
                }

                Geometry.Spatial.Plane plane = Geometry.Spatial.Create.Plane(face3D.GetBoundingBox().Min.Z + offset);

                Segment3D segment3D = Geometry.Spatial.Query.MaxIntersectionSegment3D(plane, face3D);
                if (segment3D == null) {
                    result.Add(null);
                    continue;
                }

                Line line = Geometry.Rhino.Convert.ToRhino_Line(segment3D);

                Point3d startPt = Geometry.Rhino.Convert.ToRhino(segment3D[0]);
                Point3d endPt = Geometry.Rhino.Convert.ToRhino(segment3D[1]);
                Point3d midPt = (startPt + endPt) / 2;

                Vector3d tangentVec = line.UnitTangent;
                Vector3d perpendicularVec = Vector3d.CrossProduct(tangentVec, Vector3d.ZAxis);

                global::Rhino.Geometry.Plane endPlane = new global::Rhino.Geometry.Plane(endPt, perpendicularVec, tangentVec);

                Polyline arc = new Polyline();
                Point3d arcPt = endPt + (tangentVec * maxExtend);
                arcPt.Transform(Transform.Rotation(-oneSideAngle, Vector3d.ZAxis, endPt));
                arc.Add(arcPt);
                for (int i = 0; i < halfResolution * 2; i++) {
                    arcPt.Transform(Transform.Rotation(singleStepAngle, Vector3d.ZAxis, endPt));
                    arc.Add(arcPt);
                }
                Mesh endRangeMesh = new Mesh();
                for (int i = 0; i < arc.Count - 1; i++) {
                    Mesh triangle = new Mesh();
                    triangle.Vertices.AddVertices(new List<Point3d>() { endPt, arc[i], arc[i + 1] });
                    triangle.Faces.AddFace(new MeshFace(0, 1, 2));
                    endRangeMesh.Append(triangle);
                }

                foreach (Point3f point3f in endRangeMesh.Vertices) {
                    endRangeMesh.VertexColors.Add(System.Drawing.Color.CadetBlue); //TODO: Think about colors
                }

                Mesh startRangeMesh = endRangeMesh.DuplicateMesh();
                startRangeMesh.Transform(Transform.Mirror(midPt, tangentVec));

                result.Add(new Tuple<Mesh, Mesh>(startRangeMesh, endRangeMesh));
                values.Add(maxExtend);
            }

            return result;
        }
    }
}