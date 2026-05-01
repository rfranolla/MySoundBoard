using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace MySoundBoard.Controls
{
    public partial class IconPickerDialog
    {
        private static readonly IReadOnlyList<SymbolRegular> AllIcons =
            Enum.GetValues<SymbolRegular>()
                .Where(s => s.ToString().EndsWith("48"))
                .OrderBy(s => s.ToString())
                .ToList();

        private static string _lastSearch = string.Empty;

        public SymbolRegular? SelectedSymbol { get; private set; }

        private readonly Action<SymbolRegular> _previewCallback;
        private readonly SymbolRegular _originalSymbol;

        public IconPickerDialog(SymbolRegular currentSymbol, Action<SymbolRegular> previewCallback)
        {
            InitializeComponent();
            _originalSymbol = currentSymbol;
            _previewCallback = previewCallback;
            SelectedSymbol = currentSymbol;

            SearchBox.Text = _lastSearch;
            ApplyFilter(_lastSearch);
            IconList.SelectedItem = currentSymbol;
            IconList.ScrollIntoView(currentSymbol);
        }

        private void ApplyFilter(string text)
        {
            var filtered = string.IsNullOrWhiteSpace(text)
                ? (IEnumerable<SymbolRegular>)AllIcons
                : AllIcons.Where(s => s.ToString().Contains(text, StringComparison.OrdinalIgnoreCase));
            IconList.ItemsSource = filtered.ToList();
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _lastSearch = SearchBox.Text;
            ApplyFilter(_lastSearch);
        }

        private void IconList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IconList.SelectedItem is SymbolRegular symbol)
            {
                SelectedSymbol = symbol;
                _previewCallback(symbol);
            }
        }

        private void SelectButton_Click(object sender, System.Windows.RoutedEventArgs e) => DialogResult = true;

        private void CancelButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _previewCallback(_originalSymbol);
            DialogResult = false;
        }
    }
}
