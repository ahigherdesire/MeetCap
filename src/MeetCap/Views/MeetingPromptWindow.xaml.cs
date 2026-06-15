using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MeetCap.Views;

/// <summary>
/// A lightweight, modeless toast-style prompt that slides up from the bottom-right corner.
/// Returns the chosen button's <c>Result</c> (or a default) via <see cref="ShowAndWaitAsync"/>.
/// </summary>
public partial class MeetingPromptWindow : Window
{
    private readonly TaskCompletionSource<string> _tcs = new();
    private readonly string _defaultResult;
    private readonly DispatcherTimer _autoDismiss;
    private bool _closing;

    public MeetingPromptWindow(string title, string message,
        IReadOnlyList<PromptButton> buttons, string defaultResult,
        TimeSpan? autoDismissAfter = null)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        _defaultResult = defaultResult;

        BuildButtons(buttons);

        _autoDismiss = new DispatcherTimer { Interval = autoDismissAfter ?? TimeSpan.FromSeconds(45) };
        _autoDismiss.Tick += (_, _) => Complete(_defaultResult);

        Loaded += OnLoaded;
    }

    private void BuildButtons(IReadOnlyList<PromptButton> buttons)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            var def = buttons[i];
            var btn = new Button
            {
                Content = new TextBlock
                {
                    Text = def.Text,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                },
                MinHeight = 40,
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, i == 0 ? 0 : 8, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 13.5,
                FontWeight = def.IsPrimary ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = def.IsPrimary
                    ? Brushes.White
                    : new SolidColorBrush(Color.FromArgb(0xEE, 0xFF, 0xFF, 0xFF)),
                Background = def.IsPrimary
                    ? new SolidColorBrush(Color.FromRgb(0xE8, 0x3E, 0x48))
                    : new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(def.IsPrimary ? 0 : 1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF)),
                Template = RoundedButtonTemplate(),
                Tag = def.Result,
            };
            btn.Click += (_, _) => Complete((string)btn.Tag);
            ButtonPanel.Children.Add(btn);
        }
    }

    private static ControlTemplate RoundedButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
        { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush")
        { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness")
        { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetBinding(ContentPresenter.MarginProperty, new System.Windows.Data.Binding("Padding")
        { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.AppendChild(content);
        template.VisualTree = border;
        return template;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionBottomRight();
        AnimateIn();
        _autoDismiss.Start();
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width;
        Top = area.Bottom - ActualHeight;
    }

    private void AnimateIn()
    {
        var area = SystemParameters.WorkArea;
        double targetTop = area.Bottom - ActualHeight;
        Top = targetTop + 40;
        Opacity = 0;

        var slide = new DoubleAnimation(targetTop + 40, targetTop, TimeSpan.FromMilliseconds(280))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280));
        BeginAnimation(TopProperty, slide);
        BeginAnimation(OpacityProperty, fade);
    }

    public Task<string> ShowAndWaitAsync()
    {
        Show();
        return _tcs.Task;
    }

    private void Complete(string result)
    {
        if (_closing) return;
        _closing = true;
        _autoDismiss.Stop();

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
        fade.Completed += (_, _) =>
        {
            _tcs.TrySetResult(result);
            Close();
        };
        BeginAnimation(OpacityProperty, fade);
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoDismiss.Stop();
        _tcs.TrySetResult(_defaultResult);
        base.OnClosed(e);
    }
}
