using System;
using SWF = System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Rhino.Geometry;
using Grasshopper;
using GH_IO.Serialization;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using UI;
using Eto.Forms;
using Eto.Drawing;


namespace Copilot
{
    public class PopupChatComponent : GH_Component
    {
        //private static PopupChatForm _chatForm = null;
        private string _lastResponse = "";
        public Popup Sidebar;
        public TextBox UserInput;
        public PopupChatComponent() : base(
            "Popup Chat",
            "Chat",
            "Chat interface that appears with Ctrl+,",
            "Custom",
            "Communication")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // No inputs needed as it's triggered by keyboard shortcut
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Response", "Output", "The response from the chat interface", GH_ParamAccess.item);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            UI.Settings.Instance.General.MainWindowHandle = Grasshopper.Instances.ActiveCanvas.Handle;
            UI.StyleSetter.SetWindowStyles();
            Grasshopper.Instances.ActiveCanvas.KeyDown -= Canvas_KeyDown;
            Grasshopper.Instances.ActiveCanvas.KeyDown += Canvas_KeyDown;

            GH_Canvas canvas = Grasshopper.Instances.ActiveCanvas;
            canvas.ClientSizeChanged -= Canvas_ClientSizeChanged;
            canvas.ClientSizeChanged += Canvas_ClientSizeChanged;

        }

        public override void RemovedFromDocument(GH_Document document)
        {
            Grasshopper.Instances.ActiveCanvas.KeyDown -= Canvas_KeyDown;
            //if (_chatForm != null && !_chatForm.IsDisposed)
            //{
            //    _chatForm.Close();
            //    _chatForm = null;
            //}
            base.RemovedFromDocument(document);
        }

        private void Canvas_KeyDown(object sender, SWF.KeyEventArgs e)
        {
            // Check for Ctrl+, shortcut
            if (e.Control && e.KeyCode == SWF.Keys.Oemcomma)
            {
                e.Handled = true;
                ShowChatForm();
            }
        }

        private void ShowChatForm()
        {
            Sidebar?.Close();


            var overallLayout = new DynamicLayout
            {
                Padding = new Padding(14),
                Spacing = new Size(4, 4),
                BackgroundColor = Eto.Drawing.Colors.Transparent
            };

            overallLayout.BeginHorizontal();
            //overallLayout.Add(new Panel { Width = 200,Height = 500 });
            overallLayout.Add(new LabelBuilder("      Copilot")
                .AddStyle(LabelFont.Header1)
                .SetTextColor(Eto.Drawing.Colors.White)
                .SetTextAlignment(TextAlignment.Left)
                .Build(), xscale: true);
            overallLayout.Add(new CustomButtonBuilder(UI.IconServer.GetDeleteIcon())
                            .SetStyle(VisualStyle.Circle)
                            .SetClickAction((b) => { Sidebar?.Close(); })
                            .SetBackgroundColor(Eto.Drawing.Colors.Transparent)
                            .Build(), xscale: false);
            overallLayout.EndBeginHorizontal();
            var text = new LabelBuilder("TestingTesting").AddStyle(LabelFont.Content).SetTextAlignment(TextAlignment.Center).Build();
            overallLayout.Add(new Panel { Content = text, Width = 200, Height = 500 });
            overallLayout.EndBeginHorizontal();
            UserInput = new TextBox { Width = 200, Height = 30, BackgroundColor = new Color(255, 255, 255, 155), TextColor = Eto.Drawing.Colors.White };
            UserInput.KeyDown -= TextBox_KeyDown;
            UserInput.KeyDown += TextBox_KeyDown;
            overallLayout.Add(UserInput);

            var submitButton = new CustomButtonBuilder
                                (new LabelBuilder("Submit")
                                .AddStyle(LabelFont.Header1)
                                .SetTextAlignment(TextAlignment.Center).Build())
                                .SetClickAction((b) => { Commit(); })
                                .SetPadding(new Padding(6))
                                .Build();

            overallLayout.Add(submitButton);
            UI.Settings.Instance.General.MainWindowHandle = Grasshopper.Instances.ActiveCanvas.Handle;

            Sidebar = new PopupBuilder(overallLayout)
                        //.SetBackgroundMovable()
                        .SetBackgroundColor(new Color(255, 255, 255, 155))
                        .SetBorderColor(Eto.Drawing.Colors.Transparent)
                        .AddRoundCorners(6f)
                        .SetOffset(new Eto.Drawing.Point(-12, 22))
                        .DockToApplicationWindow()
                        .SetPosition(PopupPosition.Right)
                        .Build();


            Sidebar.Show();
            UserInput.Focus();

        }

        private void Canvas_ClientSizeChanged(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Keys.Enter)
            {
                Commit();
                e.Handled = true;

            }
            else if (e.Key == Keys.Escape)
            {
                UserInput.Text = "";
                e.Handled = true;
            }
        }

        private void AddPointComponentToCanvas()
        {
            try
            {
                // Get active document
                GH_Document doc = Grasshopper.Instances.ActiveCanvas.Document;
                if (doc == null)
                {
                    System.Diagnostics.Debug.WriteLine("Document is null");
                    return;
                }

                // Create the Point component from the default library
                // Look for the specific point component by name and category
                IGH_Component pointComponent = null;
                foreach (var obj in Instances.ComponentServer.ObjectProxies)
                {
                    // Look for a component that's specifically for creating points
                    // Typically this would be in "Vector" or "Params" category with "Point" or "Pt" in the name
                    if ((obj.Desc.Category == "Vector" || obj.Desc.Category == "Params") &&
                        (obj.Desc.Name == "Point" || obj.Desc.Name.Contains("Construct Point")))
                    {
                        System.Diagnostics.Debug.WriteLine($"Found component: {obj.Desc.Name} in {obj.Desc.Category} with GUID {obj.Guid}");
                        pointComponent = Instances.ComponentServer.EmitObject(obj.Guid) as IGH_Component;
                        if (pointComponent != null) break;
                    }
                }

                if (pointComponent == null)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to find point component");

                    // Fallback to a specific GUID for the Construct Point component
                    // This is a common GUID for the Construct Point component
                    Guid constructPointGuid = new Guid("57da07bd-ecab-415d-9d86-af36d7073abc");
                    pointComponent = Instances.ComponentServer.EmitObject(constructPointGuid) as IGH_Component;

                    if (pointComponent == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to create point component using fallback GUID");
                        return;
                    }
                }

                // Get a reference to the current component (this) on the canvas
                System.Drawing.PointF pivot = new System.Drawing.PointF(
                    Attributes.Pivot.X + 150, // Position to the right of this component
                    Attributes.Pivot.Y);

                // Record undo event
                doc.UndoUtil.RecordAddObjectEvent("Add Point Component", pointComponent);

                // Add the component to the document
                doc.AddObject(pointComponent, false);

                // Position the component
                pointComponent.Attributes.Pivot = pivot;

                // Create a random point
                Random rnd = new Random();
                double x = rnd.NextDouble() * 10;
                double y = rnd.NextDouble() * 10;
                double z = rnd.NextDouble() * 10;

                // Set the x, y, z input values if this is a Construct Point component
                if (pointComponent.Params.Input.Count >= 3)
                {
                    // Assuming this is a Construct Point component with X, Y, Z inputs
                    var xParam = pointComponent.Params.Input[0];
                    var yParam = pointComponent.Params.Input[1];
                    var zParam = pointComponent.Params.Input[2];

                    xParam.AddVolatileData(new GH_Path(0), 0, new GH_Number(x));
                    yParam.AddVolatileData(new GH_Path(0), 0, new GH_Number(y));
                    zParam.AddVolatileData(new GH_Path(0), 0, new GH_Number(z));
                }
                else if (pointComponent.Params.Input.Count == 1)
                {
                    // Assuming this is a Point component with a single Point3d input
                    var param = pointComponent.Params.Input[0];
                    var pointData = new GH_Point(new Point3d(x, y, z));
                    param.AddVolatileData(new GH_Path(0), 0, pointData);
                }

                // Force a proper refresh
                pointComponent.ExpireSolution(true);
                doc.NewSolution(true);
                Instances.ActiveCanvas.Refresh();

                System.Diagnostics.Debug.WriteLine("Point component added successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error adding point component: " + ex.Message);
            }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            DA.SetData(0, _lastResponse);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get { return null; }
        }
        private void Commit()
        {
            string message = UserInput.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;


            // For now, just return "Hello" as the response
            //string response = "Hello";

            // Notify the component that a response was generated
            //ResponseGenerated?.Invoke(this, response);

            // Close the form
            //this.Close();
            UserInput.Text = "";

        }
        public override Guid ComponentGuid
        {
            get { return new Guid("e3b5c8f7-8d4b-4c9b-9c1e-2f3a9b7e6d3f"); }
        }
    }


}