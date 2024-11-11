using System.Windows;
using System.Windows.Media;

namespace SRDS.Model;
public interface IPlaceableWithArea : IPlaceable {
    public UIElement BuildArea();
}
public interface IPlaceable : System.ComponentModel.INotifyPropertyChanged {
    public UIElement? Build();
    public Point Position { get; set; }
    [System.Xml.Serialization.XmlIgnore]
    public Color Color { get; set; }
}
