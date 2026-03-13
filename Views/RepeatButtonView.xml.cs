using System;

namespace InkkSlinger;

public partial class RepeatButtonView : UserControl
{
    public RepeatButtonView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("RepeatButton");
        }
    }
}




