using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SRDS.Model.Environment;
using Map;
public class SnowCloud : IPlaceable {
    public double Intensity { get; set; } = 0;
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
    public double Radius { get; init; }
    public Vector Velocity { get; set; }
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public bool Finished { get; set; }
    public Color Color { get; set; }
    public SnowCloud() {
        Color = Colors.Black;
    }
    public SnowCloud(Point p, double r, Vector v, double intensity, DateTime start, DateTime end) : this() {
        Position = p;
        Radius = r;
        Velocity = v;
        Start = start;
        End = end;
        Intensity = intensity;
        if (intensity < 0.01)
            Color = Colors.LightGray;
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

    public void Simulate(TacticalMap map) {
        double S = Radius * Radius * Math.PI;

        Position += Velocity;
        var roads = map.Roads.Where(p => p.DistanceToRoad(Position) < Radius).ToArray();
        for (int i = 0; i < roads.Length; i++) {
            double d = roads[i].DistanceToRoad(Position),
                intersect_length = (Radius * (Radius + d) - 2 * d * d) / Math.Sqrt(Radius * Radius - d * d),
                Si = intersect_length * roads[i].Height;
            roads[i].Snowness += Intensity * Si / S;
        }
    }
}
