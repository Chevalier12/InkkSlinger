using System;

namespace InkkSlinger;

public partial class ToolBarTrayView : UserControl
{
    public ToolBarTrayView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ToolBarTray");
        }
    }
}




