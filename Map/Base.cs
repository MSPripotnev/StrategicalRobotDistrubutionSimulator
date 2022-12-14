using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Data;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Windows;
using System.Windows.Media;

namespace TacticalAgro {
    public class Base : IPlaceable {
        public Base() { }
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
        public Color Color { get; set; } = Colors.Blue;
        public Base(Point pos) {
            Position = pos;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public UIElement Build() {
            Rectangle el = new Rectangle();
            el.Width = 30;
            el.Height = 30;
            el.Fill = new SolidColorBrush(Color);
            el.Stroke = Brushes.Black;
            el.StrokeThickness = 1;
            el.Margin = new Thickness(-el.Width/2, -el.Height/2, 0, 0);
            System.Windows.Controls.Canvas.SetZIndex(el, 1);

            Binding binding = new Binding(nameof(Position) + ".X");
            binding.Source = this;
            el.SetBinding(System.Windows.Controls.Canvas.LeftProperty, binding);
            binding = new Binding(nameof(Position) + ".Y");
            binding.Source = this;
            el.SetBinding(System.Windows.Controls.Canvas.TopProperty, binding);

            return el;
        }
    }
}
