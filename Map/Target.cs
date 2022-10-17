using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro {
    public class Target : IMoveable {
        public PointF Position { get; set; }
        public Color Color { get; set; }
        public Robot ReservedRobot { get; set; } = null;
        public bool Finished { get; set; } = false;
        public Target(Point pos, Color color) {
            Position = pos;
            Color = color;
        }
        public Target(int X, int Y, Color color) {
            Position = new Point(X, Y);
            Color = color;
        }
    }
}
