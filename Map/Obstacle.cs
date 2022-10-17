using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro {
    public class Obstacle {
        private PointF[] Position { get; set; }
        public Color Color { get; set; } = Color.DarkSlateGray;
        public Obstacle(PointF[] obstacleBorders) {
            Position = obstacleBorders;
        }
        public bool PointOnObstacle(PointF testPoint) {
            bool res = false;
            int j = Position.Length - 1;
            for (int i = 0; i < Position.Length; i++) {
                if ((Position[i].Y < testPoint.Y && Position[j].Y >= testPoint.Y ||
                    Position[j].Y < testPoint.Y && Position[i].Y >= testPoint.Y) &&
                    Position[i].X + (testPoint.Y - Position[i].Y) / (Position[j].Y - Position[i].Y) * (Position[j].X - Position[i].X) < testPoint.X)
                    res = !res;
                j = i;
            }
            return res;
        }
    }
}
