using System;

namespace InkkSlinger;

public partial class ResizeGripView : UserControl
{
    public ResizeGripView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ResizeGrip");
        }
    }
}




