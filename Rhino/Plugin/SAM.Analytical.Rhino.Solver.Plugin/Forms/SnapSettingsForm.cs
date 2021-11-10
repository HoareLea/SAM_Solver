using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SAM.Analytical.Rhino.Solver.Plugin
{
    public partial class SnapSettingsForm<T> : Form where T : Core.SAMObject, Geometry.Spatial.IFace3DObject 
    {
        private List<T> face3DObjects;

        public SnapSettingsForm()
        {
            InitializeComponent();
        }

        public SnapSettingsForm(IEnumerable<T> face3DObjects)
        {
            InitializeComponent();

            if (face3DObjects != null)
            {
                this.face3DObjects = new List<T>(face3DObjects);
            }
        }

        private void Button_OK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Button_Cancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void Button_Reset_Click(object sender, EventArgs e)
        {
            face3DObjects = face3DObjects?.ConvertAll(x => Core.Query.Clone(x));

            Analytical.Solver.Modify.SetWeights(face3DObjects);
            Analytical.Solver.Modify.SetBucketSizes(face3DObjects);
            Analytical.Solver.Modify.SetMaxExtends(face3DObjects);

            DialogResult = DialogResult.OK;
            Close();
        }

        public List<T> Panels
        {
            get
            {
                if(face3DObjects != null)
                {
                    for(int i = 0; i < face3DObjects.Count; i++)
                    {
                        if(face3DObjects[i] == null)
                        {
                            continue;
                        }

                        T face3DObject = Core.Query.Clone(face3DObjects[i]);

                        if(double.TryParse(TextBox_BucketSize.Text, out double bucketSize))
                        {
                            face3DObject.SetValue(Analytical.Solver.SolverParameter.BucketSize, bucketSize);
                        }
                        
                        if(face3DObject.TryGetValue(Analytical.Solver.SolverParameter.BucketSize, out bucketSize))
                        {
                            if(double.TryParse(TextBox_BucketSizeFactor.Text, out double factor))
                            {
                                face3DObject.SetValue(Analytical.Solver.SolverParameter.BucketSize, bucketSize * factor);
                            }
                        }


                        if (double.TryParse(TextBox_MaxExtend.Text, out double maxExtend))
                        {
                            face3DObject.SetValue(Analytical.Solver.SolverParameter.MaxExtend, maxExtend);
                        }

                        if (face3DObject.TryGetValue(Analytical.Solver.SolverParameter.MaxExtend, out maxExtend))
                        {
                            if (double.TryParse(TextBox_MaxExtendFactor.Text, out double factor))
                            {
                                face3DObject.SetValue(Analytical.Solver.SolverParameter.MaxExtend, maxExtend * factor);
                            }
                        }


                        if (double.TryParse(TextBox_Weight.Text, out double weight))
                        {
                            face3DObject.SetValue(Analytical.Solver.SolverParameter.Weight, weight);
                        }

                        if (face3DObject.TryGetValue(Analytical.Solver.SolverParameter.Weight, out weight))
                        {
                            if (double.TryParse(TextBox_WeightFactor.Text, out double factor))
                            {
                                face3DObject.SetValue(Analytical.Solver.SolverParameter.Weight, weight * factor);
                            }
                        }

                        face3DObjects[i] = face3DObject;
                    }
                }
                
                return face3DObjects;
            }
        }

        private void PanelForm_Load(object sender, EventArgs e)
        {
            if(face3DObjects == null)
            {
                return;
            }

            HashSet<double> bucketSizes = new HashSet<double>();
            HashSet<double> MaxExtends = new HashSet<double>();
            HashSet<double> Weights = new HashSet<double>();
            foreach(T face3DObject in face3DObjects)
            {
                if(face3DObject == null)
                {
                    continue;
                }
                
                if(face3DObject.TryGetValue(Analytical.Solver.SolverParameter.BucketSize, out double bucketSize))
                {
                    bucketSizes.Add(bucketSize);
                }
                else
                {
                    bucketSizes.Add(double.NaN);
                }

                if (face3DObject.TryGetValue(Analytical.Solver.SolverParameter.MaxExtend, out double maxExtend))
                {
                    MaxExtends.Add(maxExtend);
                }
                else
                {
                    MaxExtends.Add(double.NaN);
                }

                if (face3DObject.TryGetValue(Analytical.Solver.SolverParameter.Weight, out double weight))
                {
                    Weights.Add(weight);
                }
                else
                {
                    Weights.Add(double.NaN);
                }
            }

            if(bucketSizes.Count == 1 && !double.IsNaN(bucketSizes.First()))
            {
                TextBox_BucketSize.Text = bucketSizes.First().ToString();
            }

            if (MaxExtends.Count == 1 && !double.IsNaN(MaxExtends.First()))
            {
                TextBox_MaxExtend.Text = MaxExtends.First().ToString();
            }

            if (Weights.Count == 1 && !double.IsNaN(Weights.First()))
            {
                TextBox_Weight.Text = Weights.First().ToString();
            }

            TextBox_BucketSizeFactor.Text = (1.0).ToString();
            TextBox_MaxExtendFactor.Text = (1.0).ToString();
            TextBox_WeightFactor.Text = (1.0).ToString();
        }

        private void TextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) &&
                (e.KeyChar != '.'))
            {
                e.Handled = true;
            }

            // only allow one decimal point
            if ((e.KeyChar == '.') && ((sender as TextBox).Text.IndexOf('.') > -1))
            {
                e.Handled = true;
            }
        }
    }
}
