//using System;
//using System.Drawing;
//// If the error persists, ensure that the `System.Windows.Forms` assembly is referenced in your project.
//// In Visual Studio, right-click on your project in the Solution Explorer, select "Add Reference",
//// go to the "Assemblies" tab, and check "System.Windows.Forms" if it is not already checked.
//using Grasshopper.Kernel;
//using Grasshopper.GUI;
//using Grasshopper.GUI.Canvas;
//using Rhino.Geometry;
//using Grasshopper;
//using GH_IO.Serialization;
//using Grasshopper.Kernel.Types;
//using Grasshopper.Kernel.Data;
//using Python.Runtime;
//using PyNet = Python.Runtime.Py;
//using System.Collections.Generic;
//using System.Xml.Linq;
////using GHPT.Configs;
////using GHPT.IO;
////using GHPT.Prompts;
////using GHPT.UI;
////using GHPT.Utils;
//using Grasshopper.Kernel.Special;
//using Rhino.FileIO;
//using System.Diagnostics;
//using Rhino.Runtime;
//using System.Reflection.Metadata;

//namespace GH.Copilot
//{
//    public class ChatComponent : GH_Component
//    {
//        //private GH_Document _doc;
//        //private PromptData _data;
//        //private readonly Spinner _spinner;

//        //public GPTConfig CurrentConfig;

//        private string previousPrompt = string.Empty;

//        private bool allowDupPrompt = false;

//        public bool PromptOverride
//        {
//            get { return allowDupPrompt; }
//            set { allowDupPrompt = value; }
//        }

//        //private static PopupChatForm _chatForm = null;
//        private string _lastResponse = "";

//        public ChatComponent() : base(
//            "Chat",
//            "Chat",
//            "Chat interface that appears with Ctrl+,",
//            "Copilot",
//            "Communication")
//        {
//            try
//            {
//                string pythonDllPath = @"C:\Program Files\Python313\python313.dll";

//                // Set the Python DLL path before initializing the runtime
//                Runtime.PythonDLL = pythonDllPath;

//                // Initialize the Python runtime
//                PythonEngine.Initialize();
//            }
//            catch (Exception ex)
//            {
//                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Python initialization failed: " + ex.Message);
//            }
//        }

//        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
//        {
//            pManager.AddTextParameter("Query", "Q", "Text query in the form of a prompt.", GH_ParamAccess.item);
//        }

//        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
//        {
//            pManager.AddTextParameter("Response", "R", "The response from the chat interface.", GH_ParamAccess.item);
//        }

//        //public override void AddedToDocument(GH_Document document)
//        //{
//        //    base.AddedToDocument(document);
//        //    Grasshopper.Instances.ActiveCanvas.KeyDown += Canvas_KeyDown;
//        //}

//        //public override void RemovedFromDocument(GH_Document document)
//        //{
//        //    Grasshopper.Instances.ActiveCanvas.KeyDown -= Canvas_KeyDown;
//        //    if (_chatForm != null && !_chatForm.IsDisposed)
//        //    {
//        //        _chatForm.Close();
//        //        _chatForm = null;
//        //    }
//        //    base.RemovedFromDocument(document);
//        //}

//        //private void Canvas_KeyDown(object sender, KeyEventArgs e)
//        //{
//        //    // Check for Ctrl+, shortcut
//        //    if (e.Control && e.KeyCode == Keys.Oemcomma)
//        //    {
//        //        e.Handled = true;
//        //        ShowChatForm();
//        //    }
//        //}

//        //private void ShowChatForm()
//        //{
//        //    if (_chatForm == null || _chatForm.IsDisposed)
//        //    {
//        //        // Calculate position - at the bottom center of the canvas
//        //        GH_Canvas canvas = Grasshopper.Instances.ActiveCanvas;
//        //        System.Drawing.Point location = new System.Drawing.Point(
//        //            canvas.ClientRectangle.Width / 2 - 200,
//        //            canvas.ClientRectangle.Height - 100);

//        //        _chatForm = new PopupChatForm(location);
//        //        _chatForm.ResponseGenerated += (sender, response) =>
//        //        {
//        //            _lastResponse = response;

//        //            // Add components to the canvas
//        //            AddComponents();

//        //            this.ExpireSolution(true);
//        //        };
//        //        _chatForm.Show(canvas);
//        //    }
//        //}

//        //public void AddComponents()
//        //{

//        //    if (!string.IsNullOrEmpty(_data.Advice))
//        //        this.CreateAdvicePanel(_data.Advice);

//        //    if (_data.Additions is null)
//        //        return;

//        //    // Compute tiers
//        //    Dictionary<int, List<Addition>> buckets = new Dictionary<int, List<Addition>>();

//        //    foreach (Addition addition in _data.Additions)
//        //    {
//        //        if (buckets.ContainsKey(addition.Tier))
//        //        {
//        //            buckets[addition.Tier].Add(addition);
//        //        }
//        //        else
//        //        {
//        //            buckets.Add(addition.Tier, new List<Addition>() { addition });
//        //        }
//        //    }

//        //    foreach (int tier in buckets.Keys)
//        //    {
//        //        int xIncrement = 250;
//        //        int yIncrement = 100;
//        //        float x = this.Attributes.Pivot.X + 100 + (xIncrement * tier);
//        //        float y = this.Attributes.Pivot.Y;

//        //        foreach (Addition addition in buckets[tier])
//        //        {
//        //            GraphUtil.InstantiateComponent(_doc, addition, new System.Drawing.PointF(x, y));
//        //            y += yIncrement;
//        //        }
//        //    }
//        //}

//        protected override void SolveInstance(IGH_DataAccess DA)
//        {
//            string query = "";
//            if (!DA.GetData(, ref query)) { return; }

//            try
//            {
//                using (PyNet.GIL()) // Acquire the Global Interpreter Lock
//                {
//                    dynamic sys = PyNet.Import("sys");
//                    // Resolve %AppData% to its full path
//                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

//                    // Construct the full path to the TT folder
//                    string ttPath = System.IO.Path.Combine(appDataPath, @"Grasshopper\Libraries\CopilotScripts");

//                    // Append the resolved path to Python's sys.path
//                    sys.path.append(ttPath);

//                    dynamic _ghScript = PyNet.Import("grasshopper_component_finder");

//                    if (_ghScript == null)
//                    {
//                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The Python module was not initialized correctly.");
//                        return;
//                    }

//                    //temporary
//                    string arg1 = "";
//                    string arg2 = "";

//                    dynamic ghScriptClass = _ghScript.GetAttr("grasshopper_component_finder");
//                    dynamic ghScriptInstance = ghScriptClass.Invoke();
//                    _lastResponse = ghScriptInstance.main(query, @"C:\Users\Wileyng\source\repos\gh_copilot\GrasshopperComponent\PythonScripts\grasshopper_components.json", "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", @"C:\Users\Wileyng\source\repos\gh_copilot\GrasshopperComponent\PythonScripts\response.json");
//                }
//            }
//            catch (Exception ex)
//            {
//                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Error while executing Python function: " + ex.Message);
//                return;
//            }

//            DA.SetData(0, _lastResponse);
//        }

//        //public void CreateAdvicePanel(string advice)
//        //{
//        //    var pivot = new System.Drawing.PointF(this.Attributes.Pivot.X, this.Attributes.Pivot.Y - 250);
//        //    this.CreatePanel(advice, "Advice", pivot, System.Drawing.Color.LightBlue);
//        //}

//        //public void CreatePanel(string content, string nickName, System.Drawing.PointF pivot)
//        //{
//        //    this.CreatePanel(content, nickName, pivot, System.Drawing.Color.FromArgb(255, 255, 250, 90));
//        //}

//        //    public void CreatePanel(string content, string nickName, System.Drawing.PointF pivot, System.Drawing.Color color)
//        //    {
//        //        // Fix for CS8370: Replace target-typed object creation with explicit type instantiation
//        //        // With the following explicit instantiation:
//        //        Dictionary<int, List<Addition>> buckets = new Dictionary<int, List<Addition>>();
//        //        GH_Panel panel = new GH_Panel
//        //        {
//        //            // Fix for IDE0017: Simplify object initialization
//        //            NickName = nickName,
//        //            UserText = content,
//        //            Properties = { Colour = color }
//        //        };

//        //        _doc.AddObject(panel, false);
//        //        panel.Attributes.Pivot = pivot;
//        //    }

//        //protected override System.Drawing.Bitmap Icon
//        //{
//        //    get { return null; }
//        //}

//        public override Guid ComponentGuid
//        {
//            get { return new Guid("e3b5c8f7-8d4b-4c9b-9c1e-2f3a9b7e6d3f"); }
//        }


//        // Custom lightweight form for the popup chat interface
//        //public class PopupChatForm : Form
//        //{
//        //    private TextBox userInput;
//        //    private Button submitButton;

//        //    // Event for when a response is generated
//        //    public event EventHandler<string> ResponseGenerated;

//        //public PopupChatForm(System.Drawing.Point location)
//        //{
//        //    this.Text = "";
//        //    this.Size = new System.Drawing.Size(400, 40);
//        //    this.StartPosition = FormStartPosition.Manual;
//        //    this.Location = location;
//        //    this.FormBorderStyle = FormBorderStyle.None;
//        //    this.ShowInTaskbar = false;
//        //    this.TopMost = true;
//        //    this.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);

//        //    // User input field - one line
//        //    userInput = new TextBox();
//        //    userInput.Dock = DockStyle.Fill;
//        //    userInput.BorderStyle = BorderStyle.None;
//        //    userInput.Font = new System.Drawing.Font("Segoe UI", 10F);
//        //    userInput.BackColor = System.Drawing.Color.White;
//        //    userInput.Padding = new Padding(5);
//        //    userInput.KeyDown += (sender, e) =>
//        //    {
//        //        if (e.KeyCode == Keys.Enter)
//        //        {
//        //            ProcessInput();
//        //            e.Handled = true;
//        //            e.SuppressKeyPress = true;
//        //        }
//        //        else if (e.KeyCode == Keys.Escape)
//        //        {
//        //            this.Close();
//        //            e.Handled = true;
//        //            e.SuppressKeyPress = true;
//        //        }
//        //    };

//        //    // Submit button
//        //    submitButton = new Button();
//        //    submitButton.Text = "Submit";
//        //    submitButton.Dock = DockStyle.Right;
//        //    submitButton.Width = 70;
//        //    submitButton.FlatStyle = FlatStyle.Flat;
//        //    submitButton.FlatAppearance.BorderSize = 0;
//        //    submitButton.BackColor = System.Drawing.Color.FromArgb(0, 120, 212);
//        //    submitButton.ForeColor = System.Drawing.Color.White;
//        //    submitButton.Click += (sender, e) => ProcessInput();

//        //    // Layout panel with shadow effect
//        //    Panel mainPanel = new Panel();
//        //    mainPanel.Dock = DockStyle.Fill;
//        //    mainPanel.Padding = new Padding(1);
//        //    mainPanel.Controls.Add(userInput);
//        //    mainPanel.Controls.Add(submitButton);

//        //    // Add shadow/border effect
//        //    mainPanel.Paint += (sender, e) =>
//        //    {
//        //        e.Graphics.DrawRectangle(new System.Drawing.Pen(System.Drawing.Color.FromArgb(200, 200, 200)),
//        //            0, 0, mainPanel.Width - 1, mainPanel.Height - 1);
//        //    };

//        //    this.Controls.Add(mainPanel);

//        //    // Set focus to the input when shown
//        //    this.Shown += (sender, e) => userInput.Focus();

//        //    // Close when focus is lost
//        //    this.Deactivate += (sender, e) => this.Close();
//        //}

//        //private void ProcessInput()
//        //{
//        //    string message = userInput.Text.Trim();
//        //    if (string.IsNullOrEmpty(message)) return;

//        //    // For now, just return "Hello" as the response
//        //    string response = "Hello";

//        //    // Notify the component that a response was generated
//        //    ResponseGenerated?.Invoke(this, response);

//        //    // Close the form
//        //    this.Close();
//        //}


//    }
//}