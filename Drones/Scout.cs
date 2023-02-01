using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

using TacticalAgro.Map;

namespace TacticalAgro.Drones {
    public class Scout : IPlaceable, IDrone {
        public int InteractDistance { get; init; }
        public int ViewingDistance { get; init; }
        public double Speed { get; set; }
        public Point Position { get; set; }
        public Point TargetPosition { get; set; }
        public double DistanceToTarget { get; }
        public Color Color { get; set; }
        public List<Point> Trajectory { get; set; }
        public RobotState CurrentState { get; set; }

        public const int ViewingRange = 50;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Scout() { }
        public Scout(Point point) {
            Position = point;
            Color = Colors.Orange;
        }
        public Scout(int x, int y) { }

        public void Simulate() {

        }

        public int Compare(IPlaceable? x, IPlaceable? y) {
            throw new NotImplementedException();
        }

        public UIElement Build() {
            throw new NotImplementedException();
        }
    }
}
