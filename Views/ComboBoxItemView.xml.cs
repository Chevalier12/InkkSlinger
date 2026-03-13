using System;

namespace InkkSlinger;

public partial class ComboBoxItemView : UserControl
{
    public ComboBoxItemView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("ComboBoxItem");
        }
    }
}




