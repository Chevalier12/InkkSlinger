using System;

namespace InkkSlinger;

public partial class RichTextBoxView : UserControl
{
    public RichTextBoxView()
    {
        InitializeComponent();
        if (this.FindName("DemoHost") is ContentControl demoHost)
        {
            demoHost.Content = ControlDemoSupport.BuildSampleElement("RichTextBox");
        }
    }
}




