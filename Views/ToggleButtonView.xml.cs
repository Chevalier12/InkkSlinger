using System;

namespace InkkSlinger;

public partial class ToggleButtonView : UserControl
{
    public ToggleButtonView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ToggleButton");
        }
    }
}




