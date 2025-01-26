using System.Reflection.Metadata;
using System.Windows.Controls;

namespace MySoundBoard.Controls
{
    /// <summary>
    /// Interaction logic for AddButton.xaml
    /// </summary>
    public partial class AddButton : UserControl
    {
        public Button MainAddButton => MainButton;

        public string Title { get; set; } = "Add Button";

        public AddButton()
        {
            InitializeComponent();
        }
    }
}
