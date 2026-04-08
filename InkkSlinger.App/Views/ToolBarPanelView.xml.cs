using System;

namespace InkkSlinger;

public partial class ToolBarPanelView : UserControl
{
    public ToolBarPanelView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ToolBarPanel");
        }
    }
}




