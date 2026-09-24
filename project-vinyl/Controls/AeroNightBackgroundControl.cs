using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace ProjectVinyl.Controls;

/// <summary>
/// Custom control that renders the AeroNight animated background:
/// - Radial gradient (static, drawn first)
/// - Two sine-wave layers with different periods (12s and 18s)
/// - Twinkling star particles (50 dots, 3s twinkle cycle)
/// Driven by a DispatcherTimer at ~30 FPS.
/// </summary>
public class AeroNightBackgroundControl : Control
{
    private readonly DispatcherTimer _timer;
    private double _elapsedSeconds;

    // Star particles
    private const int StarCount = 50;
    private readonly Star[] _stars = new Star[StarCount];

    // Wave parameters
    private static readonly Color WaveColor1 = Color.FromArgb(40, 109, 213, 250); // GlowCyan @ 15% alpha
    private static readonly Color WaveColor2 = Color.FromArgb(25, 109, 213, 250); // GlowCyan @ 10% alpha

    private struct Star
    {
        public double X; // 0..1 normalized
        public double Y; // 0..1 normalized
        public double Phase; // random phase offset for twinkle
        public double Size; // radius in pixels
    }

    public AeroNightBackgroundControl()
    {
        // Initialize stars with random positions
        var rng = new Random(42); // deterministic seed for consistency
        for (int i = 0; i < StarCount; i++)
        {
            _stars[i] = new Star
            {
                X = rng.NextDouble(),
                Y = rng.NextDouble() * 0.7, // stars only in upper 70%
                Phase = rng.NextDouble() * Math.PI * 2,
                Size = 1.0 + rng.NextDouble() * 1.5
            };
        }

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
        };
        _timer.Tick += OnTick;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _elapsedSeconds += 0.033;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // 1. Radial gradient background
        var bgBrush = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 1.2, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.5, 1.2, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(1.0, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(1.0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#1A4A7A"), 0.0),
                new GradientStop(Color.Parse("#0A2540"), 0.4),
                new GradientStop(Color.Parse("#051525"), 1.0)
            }
        };
        context.FillRectangle(bgBrush, bounds);

        // 2. Animated waves (two layers)
        DrawWave(context, bounds, _elapsedSeconds / 12.0, 0.65, 40, WaveColor1);
        DrawWave(context, bounds, _elapsedSeconds / 18.0 + 1.5, 0.75, 30, WaveColor2);

        // 3. Twinkling stars
        for (int i = 0; i < StarCount; i++)
        {
            ref var star = ref _stars[i];
            double twinkle = (Math.Sin(_elapsedSeconds * 2.0 + star.Phase) + 1.0) * 0.5; // 0..1
            byte alpha = (byte)(80 + twinkle * 175); // 80..255
            var color = Color.FromArgb(alpha, 255, 255, 255);
            var brush = new SolidColorBrush(color);
            double x = star.X * bounds.Width;
            double y = star.Y * bounds.Height;
            context.DrawEllipse(brush, null, new Point(x, y), star.Size, star.Size);
        }
    }

    private static void DrawWave(DrawingContext context, Rect bounds, double phase, double yBase, double amplitude, Color color)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            double w = bounds.Width;
            double h = bounds.Height;
            double baseY = h * yBase;

            ctx.BeginFigure(new Point(0, h), false);

            // Draw sine wave across width
            const int segments = 64;
            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                double x = t * w;
                double y = baseY + Math.Sin(t * Math.PI * 3 + phase * Math.PI * 2) * amplitude;
                ctx.LineTo(new Point(x, y));
            }

            ctx.LineTo(new Point(w, h));
            ctx.EndFigure(true);
        }

        context.DrawGeometry(new SolidColorBrush(color), null, geometry);
    }
}