using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace MySoundBoard.Controls
{
    public partial class IconPickerDialog
    {
        public SymbolRegular? SelectedSymbol { get; private set; }

        private readonly Action<SymbolRegular> _previewCallback;
        private readonly SymbolRegular _originalSymbol;

        public IconPickerDialog(SymbolRegular currentSymbol, Action<SymbolRegular> previewCallback)
        {
            InitializeComponent();
            _originalSymbol = currentSymbol;
            _previewCallback = previewCallback;
            SelectedSymbol = currentSymbol;

            var icons = Enum.GetValues<SymbolRegular>()
                .Where(s => s.ToString().EndsWith("48"))
                .OrderBy(s => s.ToString())
                .ToList();

            IconList.ItemsSource = icons;
            IconList.SelectedItem = currentSymbol;
            IconList.ScrollIntoView(currentSymbol);
        }

        private void IconList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IconList.SelectedItem is SymbolRegular symbol)
            {
                SelectedSymbol = symbol;
                _previewCallback(symbol);
            }
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _previewCallback(_originalSymbol);
            DialogResult = false;
        }
    }
}
