using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TacticalAgro {
    public interface IPlaceable : System.ComponentModel.INotifyPropertyChanged {
        public UIElement Build();
        public Point Position { get; set; }
        public Color Color { get; set; }
    }
}
