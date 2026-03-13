using System;

namespace InkkSlinger;

public partial class ToolTipView : UserControl
{
    public ToolTipView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ToolTip");
        }
    }
}




