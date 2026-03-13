using System;

namespace InkkSlinger;

public partial class ProgressBarView : UserControl
{
    public ProgressBarView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ProgressBar");
        }
    }
}




