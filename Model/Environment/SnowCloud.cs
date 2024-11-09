using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SRDS.Model.Environment;
using Map;
public class SnowCloud : IPlaceable {
    public double MaxIntensity { get; init; } = 0;
    private double intensity = 0;
    public double Intensity {
        get => intensity;
        set {
            intensity = value;
            if (intensity < 0.02)
                Color = Colors.LightGray;
            else if (intensity < 0.1)
                Color = Colors.Gray;
        }
    }
    private Point position;
    /// <summary>
    /// Center position
    /// </summary>
    public Point Position {
        get => position;
        set {
            position = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UIPosition)));
        }
    }
    /// <summary>
    /// Draw start position offset
    /// </summary>
    public Point UIPosition {
        get => position - new Vector(Width / 2, Length / 2);
    }
    public double MaxLength { get; set; }
    public double MaxWidth { get; set; }
    private double length, width;
    public double Length {
        get => length;
        set {
            length = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Length)));
        }
    }
    public double Width {
        get => width;
        set {
            width = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Width)));
        }
    }
    public Vector Velocity { get; set; }
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public bool Finished { get; set; }
    public Color Color { get; set; }
    public UIElement? UI { get; private set; } = null;
    public SnowCloud() {
        Color = Colors.Black;
    }
    public SnowCloud(Point p, double w, double l, Vector v, double _intensity, DateTime start, DateTime end) : this() {
        Position = p;
        Velocity = v;
        MaxWidth = Width = w; // X
        MaxLength = Length = l; // Y
        Start = start;
        End = end;
        Intensity = MaxIntensity = _intensity;
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    public UIElement Build() {
        if (UI is not null)
            return UI;
        Ellipse el = new Ellipse() {
            Stroke = new SolidColorBrush(Color),
            StrokeThickness = 1,
            StrokeDashOffset = 2,
            StrokeDashArray = new DoubleCollection(new double[] { 4.0, 2.0 }),
            Width = this.Width,
            Height = this.Length,
            Uid = nameof(SnowCloud),
        };
        Binding binding = new Binding(nameof(UIPosition) + ".X");
        binding.Source = this;
        el.SetBinding(System.Windows.Controls.Canvas.LeftProperty, binding);
        binding = new Binding(nameof(UIPosition) + ".Y");
        binding.Source = this;
        el.SetBinding(System.Windows.Controls.Canvas.TopProperty, binding);
        binding = new Binding(nameof(Width));
        binding.Source = this;
        el.SetBinding(Canvas.WidthProperty, binding);
        binding = new Binding(nameof(Length));
        binding.Source = this;
        el.SetBinding(Canvas.HeightProperty, binding);
        return UI = el;
    }

    public void Simulate(object? sender, DateTime time) {
        if (sender is not GlobalMeteo meteo)
            return;

        Vector rndWind = new Vector((new Random().NextDouble() - 0.5) / 8, (new Random().NextDouble() - 0.5) / 8);
        Velocity = meteo.Wind + rndWind;
        Position += Velocity;
        double mins = (End - time).TotalMinutes,
               sizeReduceTime = (End - Start).TotalMinutes / 2,
               intensityReduceTime = (End - Start).TotalMinutes / 4;
        Intensity = mins > intensityReduceTime ?
            MaxIntensity : Math.Max(0, MaxIntensity / intensityReduceTime * mins);
        if (mins < sizeReduceTime) {
            Width = Math.Max(0, MaxWidth / sizeReduceTime * mins);
            Length = Math.Max(0, MaxLength / sizeReduceTime * mins);
        }
        Finished = Width < 0.1 || Length < 0.1 || End < time;
    }
    public bool PointInside(Point p) =>
        (p.X - Position.X) * (p.X - Position.X) / Width / Width +
        (p.Y - Position.Y) * (p.Y - Position.Y) / Length / Length <= 1;

    public bool IsOutside(TacticalMap map) {
        Point[] cloudBorders = {
            UIPosition,
            UIPosition + new Vector(Width, 0),
            UIPosition + new Vector(0, Length),
            UIPosition + new Vector(Width, Length),
        };
        return (cloudBorders.All(p => map.PointOutsideBorders(p)));
    }
}
