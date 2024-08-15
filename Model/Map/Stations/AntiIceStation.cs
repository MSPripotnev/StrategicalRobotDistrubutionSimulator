using System.Windows.Media;

namespace SRDS.Model.Map.Stations;
public class AntiIceStation : Station {
    public AntiIceStation() : base() {
        Color = Colors.CadetBlue;
    }
    public AntiIceStation(System.Windows.Point pos) : base(pos) { }
}
