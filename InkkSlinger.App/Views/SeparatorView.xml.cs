using System;

namespace InkkSlinger;

public partial class SeparatorView : UserControl
{
    public SeparatorView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Separator");
        }
    }
}




