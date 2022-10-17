using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro {
    public interface IMoveable {
        public PointF Position { get; set; }
        public Color Color { get; set; }
    }
}
