using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace HardwareMonitorWidget.ViewModels;

/// <summary>
/// Отвечает за хранение палитр кистей и применение интерполированных значений
/// к коллекции метрик. Изолирует анимационную математику от MainViewModel.
/// </summary>
internal sealed class MetricAnimator
{
    private static readonly Brush[] BarBrushPalette = CreateBarBrushPalette();
    private static readonly Brush[] TextBrushPalette = CreateTextBrushPalette();

    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(700);

    /// <summary>
    /// Применяет lerp-значения к метрикам и обновляет кисти.
    /// Вызывается из анимационного цикла (~20 FPS).
    /// </summary>
    public void UpdateFrame(
        IList<MetricViewModel> metrics,
        double[] startValues,
        double[] currentValues,
        double[] targetValues,
        DateTime animationStartUtc,
        DateTime nowUtc)
    {
        var progress = (nowUtc - animationStartUtc).TotalMilliseconds / AnimationDuration.TotalMilliseconds;
        var t = Math.Clamp(progress, 0, 1);

        for (var index = 0; index < metrics.Count; index++)
        {
            currentValues[index] = startValues[index] + ((targetValues[index] - startValues[index]) * t);
            metrics[index].Value = currentValues[index];

            var brush = GetBarBrush(currentValues[index]);
            if (!ReferenceEquals(metrics[index].BarBrush, brush))
            {
                metrics[index].BarBrush = brush;
            }

            var textBrush = GetTextBrush(currentValues[index]);
            if (!ReferenceEquals(metrics[index].TextBrush, textBrush))
            {
                metrics[index].TextBrush = textBrush;
            }
        }
    }

    private static Brush GetBarBrush(double value)
    {
        var paletteIndex = (int)Math.Round(Math.Clamp(value, 0d, 100d), MidpointRounding.AwayFromZero);
        return BarBrushPalette[paletteIndex];
    }

    private static Brush GetTextBrush(double value)
    {
        var paletteIndex = (int)Math.Round(Math.Clamp(value, 0d, 100d), MidpointRounding.AwayFromZero);
        return TextBrushPalette[paletteIndex];
    }

    private static Brush[] CreateBarBrushPalette()
    {
        var palette = new Brush[101];
        for (var i = 0; i <= 100; i++)
        {
            palette[i] = CreateProgressiveBarBrush(i);
        }
        return palette;
    }

    private static Brush[] CreateTextBrushPalette()
    {
        var palette = new Brush[101];
        for (var i = 0; i <= 100; i++)
        {
            var color = CreateProgressiveTextColor(i);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            palette[i] = brush;
        }
        return palette;
    }

    private static Color CreateProgressiveTextColor(double value)
    {
        var normalized = Math.Clamp(value / 100d, 0d, 1d);

        var green  = Color.FromRgb(30,  255, 102);
        var lime   = Color.FromRgb(166, 240, 42);
        var yellow = Color.FromRgb(255, 200, 70);
        var red    = Color.FromRgb(255, 46,  79);

        if (normalized < 0.45)
            return LerpColor(green, lime, normalized / 0.45);

        if (normalized < 0.8)
            return LerpColor(lime, yellow, (normalized - 0.45) / 0.35);

        return LerpColor(yellow, red, (normalized - 0.8) / 0.2);
    }

    private static Color LerpColor(Color start, Color end, double t)
    {
        var clampedT = Math.Clamp(t, 0d, 1d);

        byte Interpolate(byte left, byte right) =>
            (byte)Math.Round(left + ((right - left) * clampedT), MidpointRounding.AwayFromZero);

        return Color.FromRgb(
            Interpolate(start.R, end.R),
            Interpolate(start.G, end.G),
            Interpolate(start.B, end.B));
    }

    private static Brush CreateProgressiveBarBrush(double value)
    {
        var normalized = Math.Clamp(value / 100d, 0d, 1d);

        var green  = Color.FromRgb(30,  255, 102);
        var lime   = Color.FromRgb(166, 240, 42);
        var yellow = Color.FromRgb(255, 200, 70);
        var red    = Color.FromRgb(255, 46,  79);

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint   = new Point(1, 0.5)
        };

        if (normalized < 0.45)
        {
            brush.GradientStops.Add(new GradientStop(green, 0));
            brush.GradientStops.Add(new GradientStop(lime,  1));
        }
        else if (normalized < 0.8)
        {
            brush.GradientStops.Add(new GradientStop(green,  0));
            brush.GradientStops.Add(new GradientStop(lime,   0.6));
            brush.GradientStops.Add(new GradientStop(yellow, 1));
        }
        else
        {
            brush.GradientStops.Add(new GradientStop(green,  0));
            brush.GradientStops.Add(new GradientStop(lime,   0.45));
            brush.GradientStops.Add(new GradientStop(yellow, 0.72));
            brush.GradientStops.Add(new GradientStop(red,    1));
        }

        brush.Freeze();
        return brush;
    }
}
