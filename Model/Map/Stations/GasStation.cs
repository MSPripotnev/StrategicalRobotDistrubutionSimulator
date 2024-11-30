using System.Windows.Media;

using SRDS.Direct.Agents;

namespace SRDS.Model.Map.Stations;
public interface IRefueller {
    public void Refuel(Agent agent) {
        agent.Fuel = Math.Min(100, agent.Fuel + 5);
    }
}
public class GasStation : Station {
    public GasStation() : base() {
        Color = Colors.Gold;
    }
    public GasStation(System.Windows.Point pos) : base(pos) {
        Color = Colors.Gold;
    }
}
