using System;

namespace InkkSlinger;

public partial class CanvasView : UserControl
{
    public CanvasView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Canvas");
        }
    }
}




