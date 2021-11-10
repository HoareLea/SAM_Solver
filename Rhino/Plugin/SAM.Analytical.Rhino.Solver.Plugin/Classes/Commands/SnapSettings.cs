using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Rhino.Solver.Plugin
{
    public class SnapSettings : Command
    {
        public SnapSettings()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static SnapSettings Instance { get; private set; }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "SAM_SnapSettings";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            Result result = RhinoGet.GetMultipleObjects("Select Panels", false, ObjectType.Brep, out ObjRef[] obj_refs);
            if (result != Result.Success || obj_refs == null)
                return result;

            List<Tuple<Panel, Brep>> tuples = new List<Tuple<Panel, Brep>>();
            foreach (var obj_ref in obj_refs)
            {
                Brep brep = obj_ref.Brep();
                if (brep != null && brep.HasUserData)
                {
                    string @string = brep.GetUserString("SAM");
                    if (string.IsNullOrWhiteSpace(@string))
                    {
                        continue;
                    }

                    List<Panel> panels_Temp = Core.Convert.ToSAM<Panel>(@string);
                    if (panels_Temp == null)
                    {
                        continue;
                    }

                    foreach (Panel panel in panels_Temp)
                    {
                        if (panel == null)
                        {
                            continue;
                        }

                        tuples.Add(new Tuple<Panel, Brep>(panel, brep));
                    }
                }
            }

            if (tuples == null || tuples.Count == 0)
            {
                return Result.Nothing;
            }

            List<Panel> panels = new List<Panel>();
            using (SnapSettingsForm<Panel> snapSettingsForm = new SnapSettingsForm<Panel>(tuples.ConvertAll(x => x.Item1)))
            {
                if (snapSettingsForm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return Result.Cancel;
                }

                panels = snapSettingsForm.Panels;
            }

            for (int i = 0; i < panels.Count; i++)
            {
                Panel panel = panels[i];
                if (panel == null)
                {
                    continue;
                }

                Tuple<Panel, Brep> tuple = tuples[i];
                if (tuple == null)
                {
                    continue;
                }

                string @string = panel.ToJObject()?.ToString();
                if (string.IsNullOrWhiteSpace(@string))
                {
                    continue;
                }

                tuple.Item2.SetUserString("SAM", @string);

            }

            return Result.Success;
        }
    }
}
