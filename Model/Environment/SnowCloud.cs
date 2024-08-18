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
    public double MaxRadius { get; init; }
    public double Radius { get; set; }
    public Vector Velocity { get; set; }
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public bool Finished { get; set; }
    public Color Color { get; set; }
    public SnowCloud() {
        Color = Colors.Black;
    }
    public SnowCloud(Point p, double r, Vector v, double _intensity, DateTime start, DateTime end) : this() {
        Position = p;
        Radius = MaxRadius = r;
        Velocity = v;
        Start = start;
        End = end;
        Intensity = MaxIntensity = _intensity;
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    public UIElement Build() {
        Ellipse el = new Ellipse() {
            Stroke = new SolidColorBrush(Color),
            StrokeThickness = 1,
            StrokeDashOffset = 2,
            StrokeDashArray = new DoubleCollection(new double[] { 4.0, 2.0 }),
            Margin = new Thickness(-Radius, -Radius, 0, 0),
            Width = Radius * 2,
            Height = Radius * 2,
            Uid = "cloud",
        };
        Binding binding = new Binding(nameof(Position) + ".X");
        binding.Source = this;
        el.SetBinding(System.Windows.Controls.Canvas.LeftProperty, binding);
        binding = new Binding(nameof(Position) + ".Y");
        binding.Source = this;
        el.SetBinding(System.Windows.Controls.Canvas.TopProperty, binding);
        return el;
    }

    public void Simulate() {
        Position += Velocity;
    }

    public bool IsOutside(TacticalMap map) {
        Point[] cloudBorders = {
            Position - new Vector(Radius, 0),
            Position - new Vector(-Radius, 0),
            Position - new Vector(0, Radius),
            Position - new Vector(0, -Radius),
        };
        return (cloudBorders.All(p => map.PointOutsideBorders(p)));
    }
}
