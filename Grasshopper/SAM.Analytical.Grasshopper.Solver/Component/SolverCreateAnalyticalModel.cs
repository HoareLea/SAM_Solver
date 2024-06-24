using Grasshopper.Kernel;
using Rhino.Geometry;
using SAM.Analytical.Grasshopper.Solver.Properties;
using SAM.Core.Grasshopper;
using System;
using System.Collections.Generic;
using SAM.Core;
using Grasshopper;
using Grasshopper.Kernel.Data;
using System.Linq;
using SAM.Analytical.Solver;
using SAM.Geometry.Grasshopper;
using SAM.Geometry.Spatial;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.Planar;
using System.Runtime.Remoting.Messaging;

namespace SAM.Analytical.Grasshopper.Solver.Component
{
    public class SolverCreateAnalyticalModel : GH_SAMVariableOutputParameterComponent
    {
        public override Guid ComponentGuid => new Guid("1d706c24-0285-473a-bffe-223caa44d611");


        /// <summary>
        /// The latest version of this component
        /// </summary>
        public override string LatestComponentVersion => "1.0.0";

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Resources.SAM_Solver;

        public SolverCreateAnalyticalModel()
            : base("Solver.CreateAnalyticalModel", "Solver.CreateAnalyticalModel", "SAM Solver", "SAM", "Solver")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override GH_SAMParam[] Inputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooSAMGeometryParam() { Name = "_shells", NickName = "_shells", Description = "Shells", Access = GH_ParamAccess.list }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooPanelParam() { Name = "panels_", NickName = "panels", Description = "Walls represented by panels (Roofs and Floors will be added through the process). Shells will be snapped to the given panels geometry.", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooSpaceParam() { Name = "spaces_", NickName = "spaces_", Description = "Spaces", Access = GH_ParamAccess.list, Optional = true }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooLocationParam() { Name = "location_", NickName = "location_", Description = "Location", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Binding));
                result.Add(new GH_SAMParam(new GooAddressParam() { Name = "address_", NickName = "address_", Description = "Address", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Binding));

                result.Add(new GH_SAMParam(new GooProfileLibraryParam() { Name = "profileLibrary_", NickName = "profileLibrary_", Description = "ProfileLibrary", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));
                result.Add(new GH_SAMParam(new GooMaterialLibraryParam() { Name = "materialLibrary_", NickName = "materialLibrary_", Description = "MaterialLibrary", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Voluntary));


                result.Add(new GH_SAMParam(new global::Grasshopper.Kernel.Parameters.Param_Number() { Name = "tolerance_", NickName = "tolerance_", Description = "Tolerance", Access = GH_ParamAccess.item, Optional = true }, ParamVisibility.Binding));

                return result.ToArray();
            }
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override GH_SAMParam[] Outputs
        {
            get
            {
                List<GH_SAMParam> result = new List<GH_SAMParam>();
                result.Add(new GH_SAMParam(new GooAnalyticalModelParam() { Name = "analyticalModel", NickName = "analyticalModel", Description = "SAM Analytical AnalyticalModel", Access = GH_ParamAccess.item }, ParamVisibility.Binding));

                return result.ToArray();
            }
        }

        private bool CheckTolerance()
        {
            //Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            return true;
        }

        protected override void SolveInstance(IGH_DataAccess dataAccess)
        {
            int index;


            List<Shell> shells = new List<Shell>();
            index = Params.IndexOfInputParam("_shells");
            if (index == -1 || !dataAccess.GetDataList(index, shells) || shells == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid data");
                return;
            }

            List<Panel> panels = new List<Panel>();
            index = Params.IndexOfInputParam("panels_");
            if (index != -1)
            {
                dataAccess.GetDataList(index, panels);
            }

            List<Space> spaces = new List<Space>();
            index = Params.IndexOfInputParam("spaces_");
            if (index != -1)
            {
                dataAccess.GetDataList(index, spaces);
            }

            Location location = Core.Query.DefaultLocation();
            index = Params.IndexOfInputParam("location_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref location);
            }

            Address address = Core.Query.DefaultAddress();
            index = Params.IndexOfInputParam("address_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref address);
            }

            double tolerance = Tolerance.Distance;
            index = Params.IndexOfInputParam("tolerance_");
            if(index != -1)
            {
                dataAccess.GetData(index, ref tolerance);
            }

            AdjacencyCluster adjacencyCluster = Analytical.Solver.Create.AdjacencyCluster(shells, panels, spaces, tolerance);

            ProfileLibrary profileLibrary = Analytical.Query.DefaultProfileLibrary();
            index = Params.IndexOfInputParam("profileLibrary_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref profileLibrary);
            }

            IEnumerable<Profile> profiles = Analytical.Query.Profiles(adjacencyCluster, profileLibrary);
            profileLibrary = new ProfileLibrary("Default Profile Library", profiles);

            MaterialLibrary materialLibrary = Analytical.Query.DefaultMaterialLibrary();
            index = Params.IndexOfInputParam("materialLibrary_");
            if (index != -1)
            {
                dataAccess.GetData(index, ref materialLibrary);
            }

            IEnumerable<IMaterial> materials = Analytical.Query.Materials(adjacencyCluster, materialLibrary);
            materialLibrary = new MaterialLibrary("Default Material Library");
            materials?.ToList().ForEach(x => materialLibrary.Add(x));

            AnalyticalModel analyticalModel = new AnalyticalModel(null, null, location, address, adjacencyCluster, materialLibrary, profileLibrary);

            List<string> materialNames = analyticalModel.MissingMaterialsNames();
            if(materialNames != null && materialNames.Count != 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, string.Format("{0}: {1}", "AnalyticalModel is missing following materials:", string.Join(", ", materialNames)));
            }

            Dictionary<ProfileType, List<string>> dictionary = analyticalModel.MissingProfileNameDictionary();
            if (dictionary != null && dictionary.Count != 0)
            {
                HashSet<string> profileNames = new HashSet<string>();

                foreach(List<string> names in dictionary.Values)
                {
                    names.ForEach(x => profileNames.Add(x));
                }

                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, string.Format("{0}: {1}", "AnalyticalModel is missing following profiles:", string.Join(", ", profileNames)));
            }

            index = Params.IndexOfOutputParam("analyticalModel");
            if (index != -1)
            {
                dataAccess.SetData(index, analyticalModel);
            }
        }
    }
}
