using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SkimStats.Controls;

// tiny line graph for the overlay, way cheaper than a full chart
public class Sparkline : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<Sparkline, IReadOnlyList<double>?>(nameof(Values));

    // NaN means scale to the biggest value
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Sparkline, double>(nameof(Maximum), double.NaN);

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<Sparkline, IBrush?>(nameof(Stroke), Brushes.White);

    static Sparkline()
    {
        // redraw only when one of these changes
        AffectsRender<Sparkline>(ValuesProperty, MaximumProperty, StrokeProperty);
    }

    public IReadOnlyList<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var values = Values;
        if (values is null || values.Count < 2 || Stroke is null)
            return;

        var max = double.IsNaN(Maximum) ? values.Max() : Maximum;
        if (max <= 0)
            max = 1; // all zeros, draw a flat line at the bottom

        var width = Bounds.Width;
        var height = Bounds.Height;
        var step = width / (values.Count - 1);

        // newest value on the right
        Point PointAt(int i) => new(i * step, height - Math.Clamp(values[i] / max, 0, 1) * height);

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(PointAt(0), false);
            for (int i = 1; i < values.Count; i++)
                g.LineTo(PointAt(i));
            g.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(Stroke, 1.5), geometry);
    }
}
