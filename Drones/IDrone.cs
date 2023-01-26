using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro.Drones {
    public interface IDrone : IMoveable {
        public int InteractDistance { get; init; }
        public int ViewingDistance { get; init; }
        public RobotState CurrentState { get; set; }
    }

    public interface IMoveable {
        public float Speed { get; set; }
        public List<Point> Trajectory { get; set; }
        public Point TargetPosition { get; set; }
        public double DistanceToTarget { get; }
    }
}
