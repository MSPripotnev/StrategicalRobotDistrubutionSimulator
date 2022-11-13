using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro {
    public class Scout : IMoveable, IDrone {
        public float Speed { get; set; } = 0;
        public PointF Position { get; set; }
        public Color Color { get; set; }

        public const int ViewingRange = 50;

        public Scout() { }
        public Scout(PointF point) { 
            Position = point;
            Color = Color.Orange;
        }
        public Scout(int x, int y) { }

        public void Simulate() {
            
        }

        public int Compare(IMoveable? x, IMoveable? y) {
            throw new NotImplementedException();
        }
    }
}
