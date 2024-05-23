using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

using SRDS.Map;

namespace SRDS.Agents.Drones {
    public class Scout : Agent {

        public const int ViewingRange = 50;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Scout() { }
        public Scout(Point point) {
            Position = point;
            Color = Colors.Orange;
        }
        public Scout(int x, int y) { }

        public override void Simulate() {

        }

        public int Compare(IPlaceable? x, IPlaceable? y) {
            throw new NotImplementedException();
        }
    }
}
