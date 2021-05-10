using Newtonsoft.Json.Linq;
using SAM.Core;
using SAM.Geometry.Planar;

namespace SAM.Geometry.Solver
{
    public class Grid2D : SAMObject
    {
        private Point2D origin;

        public Grid2D(Point2D origin)
            : base()
        {
            if(origin != null)
            {
                this.origin = new Point2D(origin);
            }
        }

        public Point2D Origin
        {
            get
            {
                return new Point2D(origin);
            }
        }

        public override bool FromJObject(JObject jObject)
        {
            if (!base.FromJObject(jObject))
                return false;

            origin = new Point2D(jObject.Value<JObject>("Origin"));

            return true;
        }

        public override JObject ToJObject()
        {
            JObject jObject = base.ToJObject();
            if (jObject == null)
                return jObject;

            jObject.Add("Origin", origin.ToJObject());

            return jObject;
        }
    }
}
