using System;

namespace InkkSlinger;

public partial class InkCanvasView : UserControl
{
    public InkCanvasView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("InkCanvas");
        }
    }
}




