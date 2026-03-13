using System;

namespace InkkSlinger;

public partial class ViewboxView : UserControl
{
    public ViewboxView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Viewbox");
        }
    }
}




