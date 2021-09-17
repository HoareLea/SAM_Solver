using Newtonsoft.Json.Linq;
using SAM.Core;
using SAM.Geometry.Planar;
using System.Collections.Generic;

namespace SAM.Geometry.Solver
{
    public class Grid2D : SAMObject
    {

        private List<Segment2D> segment2Ds;

        public Grid2D(IEnumerable<Segment2D> segment2Ds)
            : base()
        {
            if(segment2Ds != null)
            {
                this.segment2Ds = new List<Segment2D>();
                foreach(Segment2D segment2D in segment2Ds)
                {
                    this.segment2Ds.Add(segment2D);
                }
            }
        }

        public List<Segment2D> Segment2Ds
        {
            get
            {
                return segment2Ds?.ConvertAll(x => new Segment2D(x));
            }
        }

        public override bool FromJObject(JObject jObject)
        {
            if (!base.FromJObject(jObject))
                return false;

            segment2Ds = Create.ISAMGeometries<Segment2D>(jObject.Value<JArray>("Segment2Ds"));

            return true;
        }

        public override JObject ToJObject()
        {
            JObject jObject = base.ToJObject();
            if (jObject == null)
                return jObject;

            jObject.Add("Segment2Ds", Core.Create.JArray(segment2Ds));

            return jObject;
        }
    }
}
