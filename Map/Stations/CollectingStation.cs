using SRDS.Map.Targets;

using System.Windows;
using System.Windows.Media;

namespace SRDS.Map.Stations;
public class CollectingStation : Station {
    public int Capacity { get; set; }
    public List<Target> Targets { get; init; } = new List<Target>();
    public CollectingStation() : base() {
        Color = Colors.Blue;
    }
    public CollectingStation(Point pos) : this() {
        Position = pos;
    }
}
