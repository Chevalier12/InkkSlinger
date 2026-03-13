using System;

namespace InkkSlinger;

public partial class DockPanelView : UserControl
{
    public DockPanelView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("DockPanel");
        }
    }
}




