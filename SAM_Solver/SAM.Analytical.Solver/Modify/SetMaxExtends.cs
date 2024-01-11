using System.Collections.Generic;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.Spatial;

namespace SAM.Analytical.Solver
{
    public static partial class Modify
    {
        public static void SetMaxExtends<T>(this List<T> face3DObjects, bool @override = true, double offset = 0.1) where T: Core.IParameterizedSAMObject, IFace3DObject
        {
            if (face3DObjects == null)
            {
                return;
            }

            for(int i =0; i < face3DObjects.Count; i++)
            {
                T face3DObject = face3DObjects[i];
                if(face3DObject == null)
                {
                    continue;
                }

                double maxExtend = double.NaN;
                if (!@override && face3DObject.HasValue(SolverParameter.MaxExtend))
                {
                    continue;
                }

                double thickness = face3DObject.Thickness();
                if(double.IsNaN(thickness))
                {
                    maxExtend = 0.33;
                }
                else if (thickness > 0.29)
                {
                    maxExtend = 0.6;
                }
                else
                {
                    maxExtend = 0.5;
                }

                double length = double.NaN;

                Face3D face3D = face3DObject.Face3D;

                BoundingBox3D boundingBox3D = face3D.GetBoundingBox();

                if (boundingBox3D.Height > offset)
                {
                    Plane plane = Geometry.Spatial.Create.Plane(boundingBox3D.Min.Z + offset);

                    Segment3D segment3D = Geometry.Spatial.Query.MaxIntersectionSegment3D(plane, face3D);
                    if (segment3D != null)
                    {
                        length = segment3D.GetLength();
                    }
                }
                else
                {
                    Plane plane = face3D.GetPlane();
                    if (plane == null)
                    {
                        continue;
                    }

                    Geometry.Planar.Rectangle2D rectangle2D = Geometry.Planar.Create.Rectangle2D((plane.Convert(face3D).ExternalEdge2D as Geometry.Planar.ISegmentable2D)?.GetPoints());
                    if (rectangle2D == null)
                    {
                        continue;
                    }

                    length = System.Math.Max(rectangle2D.Height, rectangle2D.Width); 
                }

                if(double.IsNaN(length))
                {
                    length = 0;
                }

                length = 0.49 * length;

                maxExtend = System.Math.Min(length, maxExtend);

                face3DObjects[i].SetValue(SolverParameter.MaxExtend, maxExtend);
            }
        }
    }
}