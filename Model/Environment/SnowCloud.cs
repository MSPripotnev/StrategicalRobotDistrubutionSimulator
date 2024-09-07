using System.ComponentModel;
using System.Windows;
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
        }
    }
    public double MaxLength { get; set; }
    public double MaxWidth { get; set; }
    public double Length { get; set; }
    public double Width { get; set; }
    public Vector Velocity { get; set; }
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public bool Finished { get; set; }
    public Color Color { get; set; }
    private readonly double circulazing = 1;
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
        circulazing = new Random().Next(1, 4);
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    public UIElement Build() {
        Rectangle el = new Rectangle() {
            Stroke = new SolidColorBrush(Color),
            StrokeThickness = 0.5,
            StrokeDashOffset = 2,
            StrokeDashArray = new DoubleCollection(new double[] { 4.0, 2.0 }),
            Margin = new Thickness(-Width / 2, -Length / 2, 0, 0),
            Width = this.Width,
            Height = this.Length,
            RadiusX = this.Width / circulazing,
            RadiusY = this.Length / circulazing,
            Uid = nameof(SnowCloud),
        };
        Binding binding = new Binding(nameof(Position) + ".X");
        binding.Source = this;
        el.SetBinding(System.Windows.Controls.Canvas.LeftProperty, binding);
        binding = new Binding(nameof(Position) + ".Y");
        binding.Source = this;
        el.SetBinding(System.Windows.Controls.Canvas.TopProperty, binding);
        return el;
    }

    public void Simulate(Vector wind, DateTime time) {
        Position += Velocity;
        Velocity = wind;
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
            Position - new Vector(2*Length, 0),
            Position - new Vector(-2*Length, 0),
            Position - new Vector(0, 2*Width),
            Position - new Vector(0, -2*Width),
        };
        return (cloudBorders.All(p => map.PointOutsideBorders(p)));
    }
}
