using SAM.Core;
using SAM.Geometry.Planar;
using SAM.Geometry.Spatial;
using System;
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

            List<Shell> shells_Split = new List<Shell>(shells);
            shells_Split = shells_Split.FindAll(x => x != null).ConvertAll(x => new Shell(x));
            
            Geometry.Spatial.Modify.SplitCoplanarFace3Ds(shells_Split, tolerance_Distance: tolerance);
            if(shells_Split == null || shells_Split.Count == 0)
            {
                return result;
            }

            List<Panel> panels_Temp = null;

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

                panels_Temp = new List<Panel>(panels);
            }

            panels_Temp = panels_Temp?.ConvertAll(x => Analytical.Create.Panel(Guid.NewGuid(), x));

            List<Tuple<Panel, BoundingBox3D, Face3D>> tuples = new List<Tuple<Panel, BoundingBox3D, Face3D>>();

            foreach(Shell shell in shells_Split)
            {
                Shell shell_Temp = new Shell(shell);

                shell_Temp.OrientNormals();

                List<Face3D> face3Ds = shell_Temp?.Face3Ds;
                if(face3Ds == null || face3Ds.Count < 3)
                {
                    continue;
                }

                BoundingBox3D boundingBox3D = shell_Temp.GetBoundingBox();

                Space space = null;
                if(spaces != null)
                {
                    List<Space> spaces_Shell = spaces.ToList().FindAll(x => x?.Location != null && boundingBox3D.Inside(x.Location));
                    if(spaces_Shell != null && spaces_Shell.Count != 0)
                    {
                        space = spaces_Shell.Find(x => shell_Temp.Inside(x.Location));
                        if(space == null)
                        {
                            space = spaces_Shell.Find(x => shell_Temp.Inside(new Point3D(x.Location.X, x.Location.Y, x.Location.Z + 0.01)));
                        }
                    }
                }

                if(space == null)
                {
                    string name = "Space " + index.ToString();
                    space = new Space(name, shell_Temp.CalculatedInternalPoint3D());
                    index++;
                }

                double? area = shell_Temp.Section().ConvertAll(x => x?.GetArea()).FindAll(x => x != null && x.HasValue && !double.IsNaN(x.Value)).Sum();
                if(area != null && area.HasValue && !double.IsNaN(area.Value))
                {
                    space.SetValue(SpaceParameter.Area, area);
                }

                double volume = shell_Temp.Volume();
                if(!double.IsNaN(volume))
                {
                    space.SetValue(SpaceParameter.Volume, volume);
                }

                result.AddObject(space);

                foreach (Face3D face3D in face3Ds)
                {
                    if(face3D == null)
                    {
                        continue;
                    }

                    Point3D point3D = face3D.InternalPoint3D();

                    Panel panel = tuples.FindAll(x => x.Item2.InRange(point3D, Tolerance.MacroDistance)).Find(x => x.Item3.Inside(point3D, Core.Tolerance.MacroDistance))?.Item1;
                    if(panel != null)
                    {
                        result.AddRelation(panel, space);
                        continue;
                    }

                    List<Panel> panels_Face3D = Query.PanelsByFace3D(panels_Temp, face3D, 0.5, Core.Tolerance.MacroDistance, tolerance_Distance: tolerance);
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

                    tuples.Add(new Tuple<Panel, BoundingBox3D, Face3D>(panel, face3D.GetBoundingBox(), face3D));
                }
            }

            return result;
        }
    }
}