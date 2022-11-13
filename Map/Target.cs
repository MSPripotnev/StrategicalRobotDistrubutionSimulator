using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro {
    public class Target : IMoveable {
        public PointF Position { get; set; }
        public Color Color { get; set; }
        public Transporter? ReservedTransporter { get; set; } = null;
        public bool Finished { get; set; } = false;
        public Target(PointF pos, Color color) {
            Position = pos;
            Color = color;
        }
        public Target(int X, int Y, Color color) {
            Position = new PointF(X, Y);
            Color = color;
        }
        public static implicit operator PointF(Target target) {
            return new PointF(target.Position.X, target.Position.Y);
        }
    }
}
