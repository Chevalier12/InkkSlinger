using System;

namespace InkkSlinger;

public partial class ScrollBarView : UserControl
{
    public ScrollBarView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ScrollBar");
        }
    }
}




