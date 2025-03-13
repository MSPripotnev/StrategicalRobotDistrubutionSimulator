using System.Windows.Media;

using SRDS.Direct.Agents;
using SRDS.Direct.Agents.Drones;
using SRDS.Direct.Executive;

namespace SRDS.Model.Map.Stations;
public class AntiIceStation : Station, IRefueller {
    public void Refuel(Agent agent) {
        if (agent is not SnowRemover remover || !remover.Devices.Contains(SnowRemoverType.AntiIceDistributor)) return;
        remover.Devices.First(p => p.Type == SnowRemoverType.AntiIceDistributor).DeicingCurrent += Agent.FuelIncrease;
    }
    public AntiIceStation() : base() {
        Color = Colors.CadetBlue;
    }
    public AntiIceStation(System.Windows.Point pos) : base(pos) { }
}
