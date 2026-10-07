using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace MySoundBoard.Controls.Dialogs
{
    internal sealed class FadeSettingsWindow : FluentWindow
    {
        private readonly Slider _inSlider;
        private readonly Slider _outSlider;

        public double FadeIn => _inSlider.Value;
        public double FadeOut => _outSlider.Value;

        public FadeSettingsWindow(double fadeIn, double fadeOut)
        {
            Title = "Fade In / Out";
            Width = 300;
            Height = 200;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var panel = new StackPanel { Margin = new Thickness(12) };
            _inSlider = DialogControls.AddLabelledSlider(panel, fadeIn, 10, 0.5, v => $"Fade In: {v:F1}s", topMargin: 0);
            _outSlider = DialogControls.AddLabelledSlider(panel, fadeOut, 10, 0.5, v => $"Fade Out: {v:F1}s", topMargin: 8);
            DialogControls.AddOkButton(panel, this);
            Content = panel;
        }
    }

    internal sealed class AutoStopWindow : FluentWindow
    {
        private readonly Slider _slider;

        public double AutoStopSeconds => _slider.Value;

        public AutoStopWindow(double current)
        {
            Title = "Auto-Stop Timer";
            Width = 280;
            Height = 150;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var panel = new StackPanel { Margin = new Thickness(12) };
            _slider = DialogControls.AddLabelledSlider(panel, current, 300, 5,
                v => v == 0 ? "Auto-stop: Disabled" : $"Auto-stop: {v:F0}s", topMargin: 0, snap: false);
            DialogControls.AddOkButton(panel, this);
            Content = panel;
        }
    }

    internal static class DialogControls
    {
        /// <summary>Adds a caption that tracks the slider's value, followed by the slider itself.</summary>
        public static Slider AddLabelledSlider(Panel panel, double value, double max, double tick,
            Func<double, string> format, double topMargin, bool snap = true)
        {
            var label = new TextBlock { Text = format(value), Margin = new Thickness(0, topMargin, 0, 0) };
            var slider = new Slider
            {
                Minimum = 0, Maximum = max, Value = value,
                TickFrequency = tick, IsSnapToTickEnabled = snap
            };
            slider.ValueChanged += (s, e) => label.Text = format(e.NewValue);
            panel.Children.Add(label);
            panel.Children.Add(slider);
            return slider;
        }

        public static void AddOkButton(Panel panel, Window owner)
        {
            var ok = new System.Windows.Controls.Button
            {
                Content = "OK", Width = 70, IsDefault = true,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            ok.Click += (s, e) => owner.DialogResult = true;
            panel.Children.Add(ok);
        }
    }
}
