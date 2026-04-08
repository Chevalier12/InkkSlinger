using System;

namespace InkkSlinger;

public partial class ToolBarOverflowPanelView : UserControl
{
    public ToolBarOverflowPanelView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ToolBarOverflowPanel");
        }
    }
}




