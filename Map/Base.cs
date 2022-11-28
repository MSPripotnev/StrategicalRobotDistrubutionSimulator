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
using System.Windows;
using System.Windows.Media;

namespace TacticalAgro {
    public class Base : IPlaceable {
        public Base() { }
        public Point Position { get; set; }
        [System.Xml.Serialization.XmlIgnore]
        public Color Color { get; set; } = Colors.Blue;
        public Base(Point pos) {
            Position = pos;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public UIElement Build() {
            Rectangle el = new Rectangle();
            el.Width = 20;
            el.Height = 20;
            el.Fill = new SolidColorBrush(Color);
            el.Stroke = Brushes.Black;
            el.StrokeThickness = 1;
            el.Margin = new Thickness(-20, -20, 0, 0);

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
