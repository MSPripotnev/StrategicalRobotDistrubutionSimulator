using System;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace TacticalAgro {
    public class Obstacle : IPlaceable {
        [XmlArray("Points")]
        [XmlArrayItem("Point")]
        public Point[] Borders { get; init; }
        [XmlIgnore]
        public Point Position { get; set; }
        [XmlIgnore]
        public Color Color { get; set; } = Colors.Gray;
        public Obstacle(Point[] obstacleBorders) {
            Borders = obstacleBorders;
            Position = Borders[0];
        }
        public Obstacle() { }
        public event PropertyChangedEventHandler? PropertyChanged;

        public bool PointOnObstacle(Point testPoint) {
            bool pointInside = false;
            int j = Borders.Length - 1;
            for (int i = 0; i < Borders.Length; i++) {
                if ((Borders[i].Y < testPoint.Y && Borders[j].Y >= testPoint.Y ||
                    Borders[j].Y < testPoint.Y && Borders[i].Y >= testPoint.Y) &&
                    Borders[i].X + (testPoint.Y - Borders[i].Y) / (Borders[j].Y - Borders[i].Y) * (Borders[j].X - Borders[i].X) < testPoint.X)
                    pointInside = !pointInside;
                j = i;
            }
            return pointInside;
        }

        public UIElement Build() {
            UIElement res = null;
            if (Borders.Length > 0) {
                var polygon = new Polygon();
                polygon.Points = new PointCollection(Borders);
                polygon.Fill = new SolidColorBrush(Colors.DarkSlateGray);
                res = polygon;
            } else {
                var ellipse = new Ellipse();
                ellipse.Width = ellipse.Height = 5;
                ellipse.Margin = new Thickness(Position.X, Position.Y, 0,0);
                res = ellipse;
            }
            res.Uid = $"obstacle_{Borders[0]}";
            return res;
        }
    }
}
