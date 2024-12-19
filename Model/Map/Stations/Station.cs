using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Serialization;

using SRDS.Direct.Executive;

namespace SRDS.Model.Map.Stations;
[XmlInclude(typeof(AgentStation))]
[XmlInclude(typeof(GasStation))]
[XmlInclude(typeof(AntiIceStation))]
[XmlInclude(typeof(Meteostation))]
public abstract class Station : IPlaceable {
    private Point position;
    [XmlElement(nameof(Point), ElementName = "Position")]
    public Point Position {
        get { return position; }
        set {
            position = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
        }
    }
    [XmlIgnore]
    public Color Color { get; set; }
    protected BitmapImage? bitmapImage = null;
    public Station() { }
    public Station(Point pos) : this() {
        Position = pos;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public virtual void Simulate() { }
    public virtual UIElement Build() {
        Rectangle el = new Rectangle();
        el.Width = 40;
        el.Height = 40;
        if (bitmapImage is not null) {
            el.Fill = new ImageBrush(bitmapImage);
        } else {
            el.Fill = new SolidColorBrush(Color);
            el.Stroke = Brushes.Black;
            el.StrokeThickness = 1;
        }
        el.Margin = new Thickness(-el.Width / 2, -el.Height / 2, 0, 0);
        System.Windows.Controls.Panel.SetZIndex(el, 2);

        Binding binding = new Binding(nameof(Position) + ".X");
        binding.Source = this;
        el.SetBinding(System.Windows.Controls.Canvas.LeftProperty, binding);
        binding = new Binding(nameof(Position) + ".Y");
        binding.Source = this;
        el.SetBinding(System.Windows.Controls.Canvas.TopProperty, binding);

        return el;
    }
    public override int GetHashCode() => base.GetHashCode();
    public override bool Equals(object? obj) {
        return obj is Station s && obj.GetType() == this.GetType() && PathFinder.Distance(s.Position, Position) < 15;
    }
    public static bool operator==(Station? a, Station? b) {
        return (a is null && b is null) || a is not null && b is not null && PathFinder.Distance(a.Position, b.Position) < 15;
    }
    public static bool operator !=(Station? a, Station? b) {
        return !(a == b);
    }
}
