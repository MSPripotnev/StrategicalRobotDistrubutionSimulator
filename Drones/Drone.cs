using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro {
    public class Drone : IMoveable {
        public double Speed { get; set; } = 0;
        public PointF Position { get; set; }
        public Color Color { get; set; }

        public const int ViewingRange = 50;

        public Drone() { }
        public Drone(PointF point) { 
            Position = point;
            Color = Color.Orange;
        }
        public Drone(int x, int y) { }
    }
}
