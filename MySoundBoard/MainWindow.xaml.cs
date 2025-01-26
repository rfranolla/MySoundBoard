using MySoundBoard.Controls;
using NAudio.Wave;
using System.ComponentModel;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using Wpf.Ui.Controls;
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace MySoundBoard
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        private AddButton _addButton;
        private const string SoundBoardsDir = "SoundBoards";
        public EventHandler<RoutedEventArgs> ThemeChanged;
        public float Volume = 1.0f;

        public static MainWindow Instance;

        public MainWindow()
        {
            InitializeComponent();
            _addButton = new AddButton();
            _addButton.MainButton.Click += AddButton_Click;
            SoundBoardGrid.Items.Add( _addButton );
            InitDevices();

            InitializeLoadMenu();

            VolumeSlider.Value = 100;

            Instance = this;

            DataContext = this;
        }

        private void InitDevices()
        {
            var list = DirectSoundOut.Devices.ToList();

            OutputDevice.ItemsSource = list;
            OutputDevice.DisplayMemberPath = "Description";
            OutputDevice.SelectedIndex = 0;

            HeadphoneDevice.ItemsSource = list;
            HeadphoneDevice.DisplayMemberPath = "Description";
            HeadphoneDevice.SelectedIndex = 0;
        }

        private void InitializeLoadMenu()
        {
            var dirPath = $"{AppDomain.CurrentDomain.BaseDirectory}{SoundBoardsDir}";
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            // Get List of file names in the dir
            DirectoryInfo d = new DirectoryInfo(dirPath);

            foreach (var file in d.GetFiles("*.json"))
            {
                // Create menu child
                var menuItem = new MenuItem() 
                {
                    Header = file.Name.Split(".")[0],
                    DataContext = file,
                };
                menuItem.Click += LoadMenuItem_Click;
                LoadMenuItem.Items.Add( menuItem );
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var soundButton = new SoundBoardButton();
            AddButtonToGrid( soundButton );
        }

        private void AddButtonToGrid(SoundBoardButton soundButton)
        {
            SoundBoardGrid.Items.Insert(SoundBoardGrid.Items.Count - 1, soundButton);
        }

        public static DirectSoundDeviceInfo? GetSelectedOutputDevice()
        {
            return Instance.OutputDevice.SelectedItem as DirectSoundDeviceInfo;
        }

        public static DirectSoundDeviceInfo? GetSelectedHeadphoneDevice()
        {
            return Instance.HeadphoneDevice.SelectedItem as DirectSoundDeviceInfo;
        }

        public static void RemoveButton(SoundBoardButton button)
        {
            Instance.SoundBoardGrid.Items.Remove(button);
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Serialize the grid
            var jArray = new JsonArray();

            foreach (var button in SoundBoardGrid.Items) 
            {
                if (button is SoundBoardButton soundBoardButton)
                {
                    var jObj = soundBoardButton.Serialize();

                    jArray.Add(jObj);
                }
            }

            var json = jArray.ToString();
            var path = $"{AppDomain.CurrentDomain.BaseDirectory}/{SoundBoardsDir}/{SoundBoardTitle.Text}.json";
            File.WriteAllText(path, json);
        }

        private void LoadMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SoundBoardGrid.Items.Clear();
            SoundBoardGrid.Items.Add(_addButton);

            // Read the file
            if (sender is MenuItem menuItem)
            {
                var file = menuItem.DataContext as FileInfo;
                if (file != null) 
                {
                    string json = File.ReadAllText(file.FullName);
                    var jsonArray = JsonArray.Parse(json) as JsonArray;

                    // Loop through file and create new buttons per object
                    foreach (var jObj in jsonArray) 
                    {
                        SoundBoardButton newButton = new SoundBoardButton((JsonObject)jObj);
                        AddButtonToGrid(newButton);
                    }
                }
                this.SoundBoardTitle.Text = menuItem.Header.ToString();
            }
        }

        private void SortMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Clear any existing sorting first
            SoundBoardGrid.Items.SortDescriptions.Clear();

            SoundBoardGrid.Items.SortDescriptions.Add(new SortDescription("Title",
                                     ListSortDirection.Ascending));
        }

        private void DayMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DayMenuItem.IsChecked = true;
            NightMenuItem.IsChecked = false;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
              Wpf.Ui.Appearance.ApplicationTheme.Light, // Theme type
              Wpf.Ui.Controls.WindowBackdropType.Mica,  // Background type
              true                                      // Whether to change accents automatically
            );

            if (ThemeChanged != null)
                ThemeChanged.Invoke(this, new RoutedEventArgs());
        }

        private void NightMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DayMenuItem.IsChecked = false;
            NightMenuItem.IsChecked = true;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
              Wpf.Ui.Appearance.ApplicationTheme.Dark, // Theme type
              Wpf.Ui.Controls.WindowBackdropType.Mica,  // Background type
              true                                      // Whether to change accents automatically
            );

            if (ThemeChanged != null)
                ThemeChanged.Invoke(this, new RoutedEventArgs());
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Volume = (float)VolumeSlider.Value;
        }
    }
}