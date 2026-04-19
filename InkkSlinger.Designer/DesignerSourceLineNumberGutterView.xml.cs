using InkkSlinger;

namespace InkkSlinger.Designer;

public partial class DesignerSourceLineNumberGutterView : UserControl
{
    public DesignerSourceLineNumberGutterView()
    {
        InitializeComponent();
    }

    public Border BorderHost => LineNumberBorder;

    public DesignerSourceLineNumberPresenter Presenter => (DesignerSourceLineNumberPresenter)LineNumberPanel;
}