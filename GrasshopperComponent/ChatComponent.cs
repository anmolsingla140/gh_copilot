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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UI;
using Eto.Forms;
using Eto.Drawing;
using Python.Runtime;
using PyNet = Python.Runtime.Py;

namespace Copilot
{
    public class JsonResponse
    {
        public string explanation { get; set; }
        public object[] components { get; set; }
        public object[] connections { get; set; }
    }
    public class PopupChatComponent : GH_Component
    {
        //private static PopupChatForm _chatForm = null;
        private string _lastResponse = "";
        public Popup Sidebar;
        public TextBox UserInput;
        public TextArea Response;
        public string APIKey;
        public string GHInstallationLocation;
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
            pManager.AddTextParameter("API Key", "K", "API key string", GH_ParamAccess.item);
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
            if (Runtime.PythonDLL==null)
                Runtime.PythonDLL = @"C:\Users\wiley\AppData\Local\Programs\Python\Python310\python310.dll";

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            GHInstallationLocation = System.IO.Path.Combine(appDataPath, @"Grasshopper\Libraries\PythonScripts");

            PythonEngine.Initialize();

            GH_Canvas canvas = Grasshopper.Instances.ActiveCanvas;
            canvas.ClientSizeChanged -= Canvas_ClientSizeChanged;
            canvas.ClientSizeChanged += Canvas_ClientSizeChanged;

        }

        public override void RemovedFromDocument(GH_Document document)
        {
            Grasshopper.Instances.ActiveCanvas.KeyDown -= Canvas_KeyDown;

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
            overallLayout.BeginVertical();
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

            overallLayout.EndHorizontal();
            overallLayout.EndBeginVertical();
            overallLayout.BeginHorizontal();

            Response = new TextArea
            {
                AcceptsReturn=true,
                Wrap = true,
                BackgroundColor = new Color(255, 255, 255, 155),
                TextColor = Eto.Drawing.Colors.White,
                ReadOnly = true,
                Height= 500,
                Width = 400
            };
            
            overallLayout.Add(new Panel { Content = Response, Height = 500 });
            overallLayout.EndHorizontal();
            overallLayout.EndBeginVertical();
            overallLayout.BeginHorizontal();
            UserInput = new TextBox {  Height = 30, BackgroundColor = new Color(255, 255, 255, 155), TextColor = Eto.Drawing.Colors.White };
            UserInput.KeyDown -= TextBox_KeyDown;
            UserInput.KeyDown += TextBox_KeyDown;
            overallLayout.Add(UserInput,xscale:true);

            var submitButton = new CustomButtonBuilder
                                (new LabelBuilder("Submit")
                                .AddStyle(LabelFont.Header1)
                                .SetWidth(60)
                                .SetTextAlignment(TextAlignment.Center).Build())
                                .SetClickAction((b) => { Commit(); })
                                .SetPadding(new Padding(6))
                                .Build();
            overallLayout.Add(submitButton, xscale: false);
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

            Sidebar.Resizable = true;
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


        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetData(0, ref APIKey)) { return; }

            DA.SetData(0, _lastResponse);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get { return null; }
        }
        private void Commit()
        {
            //string message = UserInput.Text.Trim();
            if (string.IsNullOrEmpty(UserInput.Text)) return;

            if (Response.Text != String.Empty)
                Response.Append("\n");

            Response.Append(">  " + UserInput.Text + "\n");

            Solve(UserInput.Text);

            UserInput.Text = "";

        }

        private void Solve(string query)
        {
            try
            {
                using (PyNet.GIL()) // Acquire the Global Interpreter Lock
                {
                    dynamic sys = PyNet.Import("sys");
                    // Resolve %AppData% to its full path

                    // Append the resolved path to Python's sys.path
                    sys.path.append(GHInstallationLocation);
                    dynamic _ghScript = PyNet.Import("grasshopper_component_finder");

                    if (_ghScript == null)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The Python module was not initialized correctly.");
                        return;
                    }

                    //temporary
                    string componentsJsonPath = System.IO.Path.Combine(GHInstallationLocation, "grasshopper_components.json");
                    string responseJsonPath = System.IO.Path.Combine(GHInstallationLocation, "response.json");

                    dynamic ghScriptClass = _ghScript.GetAttr("grasshopper_component_finder");
                    dynamic ghScriptInstance = ghScriptClass.Invoke();
                    PyObject result = ghScriptInstance.main(query, componentsJsonPath, APIKey, responseJsonPath);

                    
                    JObject mainResponse = JObject.Parse(result.ToString());
                    JsonResponse jsonData = mainResponse["json_data"].ToObject<JsonResponse>();
                    if (Response.Text != String.Empty)
                        Response.Append( "\n");

                    Response.Append("<        "+jsonData.explanation + "\n") ;

                    Response.Invalidate();
                }
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Error while executing Python function: " + ex.Message);
                return;
            }
        }
        public override Guid ComponentGuid
        {
            get { return new Guid("e3b5c8f7-8d4b-4c9b-9c1e-2f3a9b7e6d3f"); }
        }
    }


}