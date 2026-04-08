using System;

namespace InkkSlinger;

public partial class TextBoxView : UserControl
{
    public TextBoxView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("TextBox");
        }
    }
}




