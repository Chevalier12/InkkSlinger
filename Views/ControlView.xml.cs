using System;

namespace InkkSlinger;

public partial class ControlView : UserControl
{
    public ControlView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Control");
        }
    }
}




