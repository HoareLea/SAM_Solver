// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.Spatial;

namespace SAM.Analytical.Solver
{
    public static partial class Modify
    {
        // Default reach applied when a panel's thickness is unknown.
        private const double MaxExtend_DefaultWhenThicknessUnknown = 0.33;
        // Wall thickness (m) above which a wall is treated as "thick" and given a longer reach.
        private const double MaxExtend_ThickWallThickness = 0.29;
        // Reach for thick walls.
        private const double MaxExtend_ThickWall = 0.6;
        // Reach for standard walls.
        private const double MaxExtend_StandardWall = 0.5;
        // Fraction of a panel's in-plane length that the extension may not exceed
        // (mirrors SnappedWall.ExtensionLimitLengthRatio).
        private const double MaxExtend_LengthRatio = 0.49;

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
                    maxExtend = MaxExtend_DefaultWhenThicknessUnknown;
                }
                else if (thickness > MaxExtend_ThickWallThickness)
                {
                    maxExtend = MaxExtend_ThickWall;
                }
                else
                {
                    maxExtend = MaxExtend_StandardWall;
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

                length = MaxExtend_LengthRatio * length;

                maxExtend = System.Math.Min(length, maxExtend);

                face3DObjects[i].SetValue(SolverParameter.MaxExtend, maxExtend);
            }
        }
    }
}