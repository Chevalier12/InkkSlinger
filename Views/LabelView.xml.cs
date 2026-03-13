using System;

namespace InkkSlinger;

public partial class LabelView : UserControl
{
    public LabelView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("Label");
        }
    }
}




