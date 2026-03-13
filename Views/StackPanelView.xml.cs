using System;

namespace InkkSlinger;

public partial class StackPanelView : UserControl
{
    public StackPanelView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("StackPanel");
        }
    }
}




