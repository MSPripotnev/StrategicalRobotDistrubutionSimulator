using SRDS.Direct.Agents;

using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace SRDS.Model.Targets;

public interface ITargetable : IPlaceable {
    [XmlIgnore]
    public Agent? ReservedAgent { get; set; }
    [XmlIgnore]
    public bool Finished { get; set; }
}

[XmlInclude(typeof(Snowdrift))]
[XmlInclude(typeof(Crop))]
public abstract class Target : ITargetable {
    private Point position;
    [XmlElement(nameof(Point), ElementName = "Position")]
    public Point Position {
        get => position;
        set {
            position = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
        }
    }
    [XmlIgnore]
    public Color Color { get; set; }
    [XmlIgnore]
    public Agent? ReservedAgent { get; set; } = null;
    [XmlIgnore]
    public bool Finished { get; set; } = false;
    public Target(Point pos) : this() {
        Position = pos;
    }
    public Target() { }

    public event PropertyChangedEventHandler? PropertyChanged;

    public virtual UIElement Build() {
        Ellipse el = new Ellipse();
        el.Width = 15;
        el.Height = 15;
        el.Fill = new SolidColorBrush(Color);
        el.Stroke = Brushes.Black;
        el.StrokeThickness = 1;
        el.Margin = new Thickness(-5, -5, 0, 0);
        System.Windows.Controls.Panel.SetZIndex(el, 4);

        Binding binding = new Binding(nameof(Position) + ".X");
        binding.Source = this;
        el.SetBinding(System.Windows.Controls.Canvas.LeftProperty, binding);
        binding = new Binding(nameof(Position) + ".Y");
        binding.Source = this;
        el.SetBinding(System.Windows.Controls.Canvas.TopProperty, binding);

        return el;
    }

    public override string ToString() => Position.ToString();
}
