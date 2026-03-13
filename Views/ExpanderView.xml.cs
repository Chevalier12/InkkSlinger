using System;

namespace InkkSlinger;

public partial class ExpanderView : UserControl
{
    public ExpanderView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Expander");
        }
    }
}




