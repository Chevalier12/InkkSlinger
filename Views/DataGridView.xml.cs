using System.Collections;

namespace InkkSlinger;

public partial class DataGridView : UserControl
{
    public DataGridView()
    {
        DemoRows = ControlDemoSupport.CreateSampleDataGridItemsSource();
        InitializeComponent();
        DataContext = this;
    }

    public IEnumerable DemoRows { get; }
}


