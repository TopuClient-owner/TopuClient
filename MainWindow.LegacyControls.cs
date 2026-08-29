using System.Windows.Controls;

namespace TopuLauncher;

public partial class MainWindow
{
    // Compatibility fields for code paths retained from the previous launcher UI.
    // The current XAML intentionally does not display these controls anymore.
    private readonly TextBox ProfileNameInput = new TextBox();
    private readonly ComboBox AuthTypeBox = new ComboBox();
}
