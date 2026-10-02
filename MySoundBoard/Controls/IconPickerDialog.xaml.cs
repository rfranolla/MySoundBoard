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
        private readonly System.Windows.Threading.DispatcherTimer _filterDebounce;

        public IconPickerDialog(SymbolRegular currentSymbol, Action<SymbolRegular> previewCallback)
        {
            InitializeComponent();
            _originalSymbol = currentSymbol;
            _filterDebounce = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _filterDebounce.Tick += (s, a) => { _filterDebounce.Stop(); ApplyFilter(_lastSearch); };
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
            _filterDebounce.Stop();
            _filterDebounce.Start();
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

        private void CancelButton_Click(object sender, System.Windows.RoutedEventArgs e) => DialogResult = false;

        // Covers Cancel, Esc and the window X: anything but Select restores the original icon.
        protected override void OnClosed(EventArgs e)
        {
            _filterDebounce.Stop();
            if (DialogResult != true)
                _previewCallback(_originalSymbol);
            base.OnClosed(e);
        }
    }
}
