using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace SRDS.Map.Stations;
[XmlInclude(typeof(AgentStation))]
[XmlInclude(typeof(GasStation))]
[XmlInclude(typeof(AntiIceStation))]
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
    public Station() { }
    public Station(Point pos) : this() {
        Position = pos;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public virtual void Simulate() { }
    public virtual UIElement Build() {
        Rectangle el = new Rectangle();
        el.Width = 20;
        el.Height = 20;
        el.Fill = new SolidColorBrush(Color);
        el.Stroke = Brushes.Black;
        el.StrokeThickness = 1;
        el.Margin = new Thickness(-el.Width / 2, -el.Height / 2, 0, 0);
        System.Windows.Controls.Panel.SetZIndex(el, 1);

        Binding binding = new Binding(nameof(Position) + ".X");
        binding.Source = this;
        el.SetBinding(System.Windows.Controls.Canvas.LeftProperty, binding);
        binding = new Binding(nameof(Position) + ".Y");
        binding.Source = this;
        el.SetBinding(System.Windows.Controls.Canvas.TopProperty, binding);

        return el;
    }
}
