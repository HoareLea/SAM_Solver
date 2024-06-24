using SAM.Geometry.Spatial;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAM.Analytical.Solver
{
    public static partial class Create
    {
        public static AdjacencyCluster AdjacencyCluster(this IEnumerable<Shell> shells, IEnumerable<Panel> panels, IEnumerable<Space> spaces, double tolerance = Core.Tolerance.Distance)
        {
            if(shells == null)
            {
                return null;
            }

            AdjacencyCluster result = new AdjacencyCluster();

            int index = 1;

            List<Shell> shells_Split = Geometry.Spatial.Query.Split(shells, tolerance_Distance: tolerance);
            if(shells_Split == null || shells_Split.Count == 0)
            {
                return result;
            }

            if(panels != null)
            {
                List<Point3D> point3Ds = new List<Point3D>();
                foreach(Panel panel in panels)
                {
                    Face3D face3D = panel?.Face3D;

                    List<Point3D> point3Ds_Face3D = face3D.Point3Ds();
                    if(point3Ds_Face3D != null)
                    {
                        point3Ds.AddRange(point3Ds_Face3D);
                    }

                }

                Parallel.For(0, shells_Split.Count, (int i) => 
                {
                    shells_Split[i] = shells_Split[i].Snap(point3Ds, Core.Tolerance.MacroDistance);
                });
            }

            foreach(Shell shell in shells_Split)
            {
                List<Face3D> face3Ds = shell?.Face3Ds;
                if(face3Ds == null || face3Ds.Count < 3)
                {
                    continue;
                }

                BoundingBox3D boundingBox3D = shell.GetBoundingBox();

                Space space = null;
                if(spaces != null)
                {
                    List<Space> spaces_Shell = spaces.ToList().FindAll(x => x?.Location != null && boundingBox3D.Inside(x.Location));
                    if(spaces_Shell != null && spaces_Shell.Count != 0)
                    {
                        space = spaces_Shell.Find(x => shell.Inside(x.Location));
                        if(space == null)
                        {
                            space = spaces_Shell.Find(x => shell.Inside(new Point3D(x.Location.X, x.Location.Y, x.Location.Z + 0.01)));
                        }
                    }
                }

                if(space == null)
                {
                    string name = "Space " + index.ToString();
                    space = new Space(name, shell.CalculatedInternalPoint3D());
                }

                result.AddObject(space);

                foreach (Face3D face3D in face3Ds)
                {
                    if(face3D == null)
                    {
                        continue;
                    }

                    Panel panel = null;

                    List<Panel> panels_Face3D = Query.PanelsByFace3D(panels, face3D, 0.5, Core.Tolerance.MacroDistance, tolerance_Distance: tolerance);
                    if(panels_Face3D != null && panels_Face3D.Count != 0)
                    {
                        panel = Analytical.Create.Panel(panels_Face3D[0].Guid, panels_Face3D[0], face3D);
                    }

                    if(panel == null)
                    {
                        Vector3D vector3D = face3D?.GetPlane()?.Normal;
                        if(vector3D != null)
                        {
                            PanelType panelType = Query.PanelType(vector3D);
                            Construction construction = Query.DefaultConstruction(panelType);
                            panel = Analytical.Create.Panel(construction, panelType, face3D);
                        }
                    }

                    result.AddObject(panel);

                    result.AddRelation(panel, space);
                }
            }

            return result;
        }
    }
}