using System.Windows.Media;

namespace SRDS.Map.Stations;
public class GasStation : Station {
    public GasStation() : base() {
        Color = Colors.Gold;
    }
    public GasStation(System.Windows.Point pos) : base(pos) {
        Color = Colors.Gold;
    }
}
