using System;

namespace InkkSlinger;

public partial class SliderView : UserControl
{
    public SliderView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Slider");
        }
    }
}




