using System;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace TacticalAgro {
    public class Obstacle : IPlaceable {
        public Point[] Borders { get; init; }
        public Point Position { get; set; }
        public Color Color { get; set; } = Colors.Gray;
        public Obstacle(Point[] obstacleBorders) {
            Borders = obstacleBorders;
            Position = Borders[0];
        }

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
            return null;
        }
    }
}
