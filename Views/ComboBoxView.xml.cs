using System;

namespace InkkSlinger;

public partial class ComboBoxView : UserControl
{
    public ComboBoxView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ComboBox");
        }
    }
}




