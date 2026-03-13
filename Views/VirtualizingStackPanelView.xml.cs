using System;

namespace InkkSlinger;

public partial class VirtualizingStackPanelView : UserControl
{
    public VirtualizingStackPanelView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("VirtualizingStackPanel");
        }
    }
}




