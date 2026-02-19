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
    private static readonly Brush[] BarBrushPalette  = CreateBarBrushPalette();
    private static readonly Brush[] TextBrushPalette = CreateTextBrushPalette();

    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(700);

    // Пороговые значения градиентной прогрессии (нормализованное значение 0–1)
    private const double GreenToLimeThreshold  = 0.45;  // зелёный → лаймовый (< 45 %)
    private const double LimeToYellowThreshold = 0.80;  // лаймовый → жёлтый  (< 80 %)
    // Позиции остановок градиента внутри бара (0 = левый край, 1 = правый)
    private const double LimeStopPointMedium   = 0.60;  // лайм на 60 % ширины бара (средний диапазон)
    private const double GreenStopPointHigh    = 0.45;  // зелёный на 45 % (высокий диапазон)
    private const double YellowStopPointHigh   = 0.72;  // жёлтый на 72 % (высокий диапазон)

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

        if (normalized < GreenToLimeThreshold)
            return LerpColor(green, lime, normalized / GreenToLimeThreshold);

        if (normalized < LimeToYellowThreshold)
            return LerpColor(lime, yellow, (normalized - GreenToLimeThreshold) / (LimeToYellowThreshold - GreenToLimeThreshold));

        return LerpColor(yellow, red, (normalized - LimeToYellowThreshold) / (1.0 - LimeToYellowThreshold));
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

        if (normalized < GreenToLimeThreshold)
        {
            brush.GradientStops.Add(new GradientStop(green, 0));
            brush.GradientStops.Add(new GradientStop(lime,  1));
        }
        else if (normalized < LimeToYellowThreshold)
        {
            brush.GradientStops.Add(new GradientStop(green,  0));
            brush.GradientStops.Add(new GradientStop(lime,   LimeStopPointMedium));
            brush.GradientStops.Add(new GradientStop(yellow, 1));
        }
        else
        {
            brush.GradientStops.Add(new GradientStop(green,  0));
            brush.GradientStops.Add(new GradientStop(lime,   GreenStopPointHigh));
            brush.GradientStops.Add(new GradientStop(yellow, YellowStopPointHigh));
            brush.GradientStops.Add(new GradientStop(red,    1));
        }

        brush.Freeze();
        return brush;
    }
}
