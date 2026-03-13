using System;

namespace InkkSlinger;

public partial class UniformGridView : UserControl
{
    public UniformGridView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("UniformGrid");
        }
    }
}




