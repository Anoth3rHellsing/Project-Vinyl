using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using ProjectVinyl.Services;

namespace ProjectVinyl.Controls;

public class AudioVisualizerControl : Control
{
    private readonly DispatcherTimer _renderTimer;
    private readonly float[] _displayBands;
    private readonly int _bandCount = 64;

    // AeroNight UI Kit colors
    private static readonly Color BarBottomColor = Color.Parse("#6DD5FA"); // GlowCyan
    private static readonly Color BarTopColor = Color.Parse("#FFFFFF");   // White tips
    private static readonly Color GlowColor = Color.Parse("#7DD5FA80");   // GlowCyan with alpha

    public static readonly StyledProperty<AudioVisualizerService?> VisualizerServiceProperty =
        AvaloniaProperty.Register<AudioVisualizerControl, AudioVisualizerService?>(nameof(VisualizerService));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<AudioVisualizerControl, bool>(nameof(IsActive));

    public AudioVisualizerService? VisualizerService
    {
        get => GetValue(VisualizerServiceProperty);
        set => SetValue(VisualizerServiceProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public AudioVisualizerControl()
    {
        _displayBands = new float[_bandCount];
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS target
        };
        _renderTimer.Tick += OnRenderTick;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsActiveProperty)
        {
            if (IsActive)
            {
                _renderTimer.Start();
            }
            else
            {
                _renderTimer.Stop();
                Array.Clear(_displayBands, 0, _displayBands.Length);
                InvalidateVisual();
            }
        }
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        var service = VisualizerService;
        if (service != null && IsActive)
        {
            var bands = service.GetBands();
            for (int i = 0; i < _bandCount && i < bands.Length; i++)
            {
                _displayBands[i] = bands[i];
            }
        }
        else
        {
            // Decay to zero when inactive
            for (int i = 0; i < _bandCount; i++)
            {
                _displayBands[i] *= 0.85f;
                if (_displayBands[i] < 0.001f) _displayBands[i] = 0f;
            }
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        double barWidth = bounds.Width / _bandCount;
        double gap = Math.Max(1, barWidth * 0.15);
        double actualBarWidth = barWidth - gap;

        if (actualBarWidth < 1) actualBarWidth = 1;

        for (int i = 0; i < _bandCount; i++)
        {
            float amplitude = _displayBands[i];
            if (amplitude < 0.001f) continue;

            double barHeight = amplitude * bounds.Height * 0.9;
            if (barHeight < 2) barHeight = 2;

            double x = i * barWidth + gap * 0.5;
            double y = bounds.Height - barHeight;

            // AeroGlass gradient brush from bottom (#1F86C8) to top (#8FD9F7)
            var gradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(BarBottomColor, 0),
                    new GradientStop(BarTopColor, 1)
                }
            };

            // Vertical bar with gradient
            context.FillRectangle(gradient, new Rect(x, y, actualBarWidth, barHeight));

            // Subtle glow cap at the top of each bar
            if (amplitude > 0.3)
            {
                context.FillRectangle(new SolidColorBrush(GlowColor), new Rect(x - 1, y - 2, actualBarWidth + 2, 4));
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(availableSize.Width, 180);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _renderTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }
}