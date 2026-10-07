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

    /// <summary>Picks the part of a sound to play. An end at the file's length means "no end trim".</summary>
    internal sealed class TrimWindow : FluentWindow
    {
        private const double MinRegionSeconds = 0.1;

        private readonly Slider _startSlider;
        private readonly Slider _endSlider;
        private readonly double _duration;

        public double TrimStart => _startSlider.Value;
        /// <summary>0 when the end slider sits at the end of the file.</summary>
        public double TrimEnd => _endSlider.Value >= _duration - 0.05 ? 0 : _endSlider.Value;

        public TrimWindow(double durationSeconds, double trimStart, double trimEnd)
        {
            _duration = durationSeconds;
            Title = "Trim Sound";
            Width = 340;
            Height = 210;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            double end = trimEnd > 0 ? Math.Min(trimEnd, durationSeconds) : durationSeconds;
            double start = Math.Clamp(trimStart, 0, Math.Max(0, end - MinRegionSeconds));

            var panel = new StackPanel { Margin = new Thickness(12) };
            _startSlider = DialogControls.AddLabelledSlider(panel, start, durationSeconds, 0.1,
                v => $"Start: {v:F1}s", topMargin: 0);
            _endSlider = DialogControls.AddLabelledSlider(panel, end, durationSeconds, 0.1,
                v => v >= durationSeconds - 0.05 ? $"End: {v:F1}s (end of file)" : $"End: {v:F1}s", topMargin: 8);

            // Keep at least a sliver of sound between the two handles.
            _startSlider.ValueChanged += (s, e) =>
            {
                if (_endSlider.Value - e.NewValue < MinRegionSeconds)
                    _endSlider.Value = Math.Min(durationSeconds, e.NewValue + MinRegionSeconds);
            };
            _endSlider.ValueChanged += (s, e) =>
            {
                if (e.NewValue - _startSlider.Value < MinRegionSeconds)
                    _startSlider.Value = Math.Max(0, e.NewValue - MinRegionSeconds);
            };

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
