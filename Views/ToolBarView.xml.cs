using System;

namespace InkkSlinger;

public partial class ToolBarView : UserControl
{
    public ToolBarView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ToolBar");
        }
    }
}




