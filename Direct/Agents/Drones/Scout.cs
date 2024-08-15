using SRDS.Model;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace SRDS.Direct.Agents.Drones;
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
