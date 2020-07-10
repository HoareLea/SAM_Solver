using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using SAM.Analytical.Grasshopper;
using SAM.Core.Grasshopper;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Solver.Grasshopper
{
    public class PanelFromSnapSolver : GH_SAMComponent
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("5857acd8-c038-4a1e-9bde-33dedfed4fde");

        public PanelFromSnapSolver() : base("PanelFromSnapSolver", "PanelFromSnapSolver", "PanelFromSnapSolver", "SAM", "Solver")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            int index;

            index = pManager.AddParameter(new GooPanelParam(), "_panels", "_panels", "Panels", GH_ParamAccess.list);
            pManager[index].DataMapping = GH_DataMapping.Flatten;

            pManager.AddBrepParameter("_surfaces", "_surfaces", "Surfaces", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("_sources", "_sources", "Sources Indexes", GH_ParamAccess.tree);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GooPanelParam(), "Panels", "P", "Panels", GH_ParamAccess.list);
            pManager.AddParameter(new GooPanelParam(), "UnusedPanels", "UP", "Unused Panels", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Analytical.Panel> panels = new List<SAM.Analytical.Panel>();
            if (!DA.GetDataList(0, panels))
                return;

            GH_Structure<GH_Brep> breps = new GH_Structure<GH_Brep>();
            if (!DA.GetDataTree(1, out breps))
                return;

            GH_Structure<GH_Integer> sources;
            if (!DA.GetDataTree(2, out sources))
                return;

            List<Tuple<int, List<int>>> tuples = new List<Tuple<int, List<int>>>();
            HashSet<int> indexes_Unique = new HashSet<int>();
            for (int i = 0; i < sources.PathCount; i++)
            {
                List<GH_Integer> goos = sources[i];
                List<int> indexes_Temp = new List<int>();
                foreach (GH_Integer goo in goos)
                {
                    int index = goo.Value;
                    indexes_Temp.Add(index);
                    indexes_Unique.Add(index);
                }
                tuples.Add(new Tuple<int, List<int>>(i, indexes_Temp));
            }

            List<Analytical.Panel> result = new List<Analytical.Panel>();
            foreach (Tuple<int, List<int>> tuple in tuples)
            {
                int index_Brep = tuple.Item1;
                if (index_Brep == -1)
                    continue;

                List<int> indexes_Panel = tuple.Item2;
                if (indexes_Panel == null || indexes_Panel.Count == 0)
                    continue;

                GH_Brep brep = breps[index_Brep][0];
                if (brep == null)
                    continue;

                List<Face3D> face3Ds = Geometry.Spatial.Query.Face3Ds(Geometry.Grasshopper.Convert.ToSAM(brep, true));
                if (face3Ds == null || face3Ds.Count == 0)
                    continue;

                Face3D face3D = face3Ds.First();

                Analytical.Panel panel_Old = panels[indexes_Panel.First()];
                if (panel_Old == null)
                    continue;

                Analytical.Panel panel_New = new Analytical.Panel(panel_Old.Guid, panel_Old, face3D, null, true, Core.Tolerance.MacroDistance, 0.3);

                result.Add(panel_New);
            }

            List<Analytical.Panel> result_Unused = new List<Analytical.Panel>();
            foreach (Analytical.Panel panel in panels)
            {
                Analytical.Panel panel_Temp = result.Find(x => x.Guid.Equals(panel.Guid));
                if (panel_Temp == null)
                    result_Unused.Add(panel);
            }

            DA.SetDataList(0, result.ConvertAll(x => new GooPanel(x)));
            DA.SetDataList(1, result_Unused.ConvertAll(x => new GooPanel(x)));
        }
    }
}