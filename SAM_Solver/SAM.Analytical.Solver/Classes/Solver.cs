using SAM.Core;
using SAM.Geometry.Spatial;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAM.Analytical.Solver
{
    public class Solver<T> where T: ISAMObject, IFace3DObject
    {
        public double Tolerance_Distance { get; set; }  = Tolerance.Distance;
        public double Tolerance_Angle { get; set; } = Tolerance.Angle;
        public double Tolerance_Arc { get; set; } = 0.3 * (System.Math.PI / 180);
        public double Offset { get; set; } = 0.2;
        public double SnapDistance { get; set; } = 0.5;
        public double MinLength { get; set; } = 0.1;

        private List<T> face3DObjects;
        private List<Range<double>> ranges;

        public Solver(IEnumerable<T> face3DObjects, IEnumerable<Range<double>> ranges)
        {
            if(face3DObjects != null)
            {
                this.face3DObjects = new List<T>(face3DObjects);
            }

            if(ranges != null)
            {
                this.ranges = new List<Range<double>>(ranges);
            }
        }

        public List<T> Execute(out List<Point3D> nakedPoint3Ds, double bucketSizeFactor = 0.20)
        {
            nakedPoint3Ds = null;

            if (face3DObjects == null || face3DObjects.Count < 2)
            {
                return null;
            }

            if(ranges == null)
            {
                ranges = face3DObjects.ElevationRanges(Tolerance_Distance);
            }

            if(ranges == null || ranges.Count == 0)
            {
                return null;
            }

            ranges.Sort((x, y) => x.Min.CompareTo(y.Min));

            List<List<T>> face3DObjectsList = Enumerable.Repeat<List<T>>(null, ranges.Count).ToList();
            List<double> minLengths = Enumerable.Repeat(double.NaN, ranges.Count).ToList();
            Parallel.For(0, ranges.Count, (int i) =>
            {
                Range<double> range = ranges[i];

                Plane plane = Geometry.Spatial.Create.Plane(range.Min + Offset);

                Dictionary<T, List<ISegmentable3D>> dictionary = face3DObjects.SectionDictionary<T, ISegmentable3D>(plane, Tolerance_Angle, Tolerance_Distance);
                if(dictionary == null || dictionary.Count == 0)
                {
                    return;
                }

                List<Point3D> nakedPoint3Ds_Temp = new List<Point3D>();
                List<Range<double>> ranges_Temp = new List<Range<double>>() { range };

                List<T> face3DObjects_Plane = new List<T>();
                foreach(KeyValuePair<T, List<ISegmentable3D>> keyValuePair in dictionary)
                {
                    face3DObjects_Plane.Add(keyValuePair.Key);
                }

                Modify.Snap(face3DObjects_Plane, null, null, null, ranges_Temp, Offset, SnapDistance, MinLength, Tolerance_Distance, Tolerance_Angle, Tolerance_Arc, out nakedPoint3Ds_Temp);

                face3DObjectsList[i] = face3DObjects_Plane;
                //if(face3DObjects_Plane != null && face3DObjects_Plane.Count != 0)
                //{
                //    double minLength = double.MaxValue;
                //    foreach(T face3Dobject in face3DObjects_Plane)
                //    {
                //        Segment3D segment3D = Geometry.Spatial.Query.MaxIntersectionSegment3D(plane, face3Dobject);
                //        if(segment3D == null || !segment3D.IsValid())
                //        {
                //            continue;
                //        }

                //        double length = segment3D.GetLength();
                //        if(double.IsNaN(length) || length == 0)
                //        {
                //            continue;
                //        }

                //        if(length < minLength)
                //        {
                //            minLength = length;
                //        }
                //    }

                //    if(minLength != double.MaxValue)
                //    {
                //        minLengths[i] = minLength;
                //    }
                //}

            });

            List<T> face3DObjects_All = new List<T>();
            foreach(List<T> face3DObjects_Temp in face3DObjectsList)
            {
                if(face3DObjects_Temp == null || face3DObjects_Temp.Count == 0)
                {
                    continue;
                }

                face3DObjects_All.AddRange(face3DObjects_Temp);
            }

            //minLengths.RemoveAll(x => double.IsNaN(x) || x <= 0);
            //double minLength_Temp = MinLength;
            //if(minLengths != null && minLengths.Count != 0)
            //{
            //    if(minLengths.Count > 1)
            //    {
            //        minLengths.Sort();
            //    }

            //    minLength_Temp = minLengths[0];
            //}

            IEnumerable<double> bucketSizes = Enumerable.Repeat(bucketSizeFactor, face3DObjects_All.Count);
            Modify.Snap(face3DObjects_All, bucketSizes, null, null, ranges, Offset, SnapDistance, MinLength * 0.8, Tolerance_Distance, Tolerance_Angle, Tolerance_Arc, out nakedPoint3Ds);
            return face3DObjects_All;
        }
    }
}
