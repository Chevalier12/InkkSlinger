using System;

namespace InkkSlinger;

public partial class TabControlView : UserControl
{
    public TabControlView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("TabControl");
        }
    }
}




