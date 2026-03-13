using System;

namespace InkkSlinger;

public partial class CheckBoxView : UserControl
{
    public CheckBoxView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("CheckBox");
        }
    }
}




