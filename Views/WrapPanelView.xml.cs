using System;

namespace InkkSlinger;

public partial class WrapPanelView : UserControl
{
    public WrapPanelView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("WrapPanel");
        }
    }
}




