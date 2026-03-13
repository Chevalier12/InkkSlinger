using System;

namespace InkkSlinger;

public partial class RadioButtonView : UserControl
{
    public RadioButtonView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("RadioButton");
        }
    }
}




