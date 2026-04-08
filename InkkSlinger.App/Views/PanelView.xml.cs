using System;

namespace InkkSlinger;

public partial class PanelView : UserControl
{
    public PanelView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Panel");
        }
    }
}




