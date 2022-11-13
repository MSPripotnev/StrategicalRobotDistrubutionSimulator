using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro {
    public class Obstacle : IMoveable {
        public PointF[] Borders { get; private set; }
        public PointF Position { get; set; }
        public Color Color { get; set; } = Color.DarkSlateGray;
        public Obstacle(PointF[] obstacleBorders) {
            Borders = obstacleBorders;
            Position = Borders[0];
        }
        public bool PointOnObstacle(PointF testPoint) {
            bool pointInside = false;
            int j = Borders.Length - 1;
            for (int i = 0; i < Borders.Length; i++) {
                if ((Borders[i].Y < testPoint.Y && Borders[j].Y >= testPoint.Y ||
                    Borders[j].Y < testPoint.Y && Borders[i].Y >= testPoint.Y) &&
                    Borders[i].X + (testPoint.Y - Borders[i].Y) / (Borders[j].Y - Borders[i].Y) * (Borders[j].X - Borders[i].X) < testPoint.X)
                    pointInside = !pointInside;
                j = i;
            }
            double distanceToObstacle = Analyzer.Distance(testPoint, Borders.MinBy(b => Analyzer.Distance(testPoint, b)));

            return pointInside || distanceToObstacle < 0;
        }
    }
}
