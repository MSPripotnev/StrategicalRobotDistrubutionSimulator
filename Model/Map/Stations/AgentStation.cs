using SRDS.Direct;
using SRDS.Direct.Agents;
using SRDS.Direct.Agents.Drones;
using SRDS.Direct.Executive;

using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace SRDS.Model.Map.Stations;

public enum SystemState {

}

public enum FuzzySystemState {

}

public class AgentStation : Station, IControllable, IRefueller {
    #region Properties
    [XmlIgnore]
    public Agent[] AssignedAgents { get; set; } = Array.Empty<Agent>();
    [XmlIgnore]
    public Road[] AssignedRoads { get; set; } = Array.Empty<Road>();
    [XmlIgnore]
    public Agent[] FreeAgents {
        get {
            return AssignedAgents.Where(x => x.CurrentState == RobotState.Ready).ToArray();
        }
    }
    #endregion

    #region State Machine
    Dictionary<FuzzySystemState, double> FuzzySystemState = new();
    private SystemState currentState;
    [XmlIgnore]
    public SystemState CurrentState {
        get => currentState;
        set {
            currentState = value;
        }
    }
    public void Simulate(object? sender, DateTime time) {
        if (sender is Director)
            Distribute();
        for (int i = 0; i < AssignedAgents.Length; i++)
            do
                AssignedAgents[i].Simulate(sender is Director ? this : sender, time);
            while (AssignedAgents[i].CurrentState == RobotState.Thinking);
    }
    #endregion

    #region Constructors
    public AgentStation() : base() {
        Color = Colors.SandyBrown;
        bitmapImage = new BitmapImage(new Uri(@"../../../Model/Map/Stations/garage.png", UriKind.Relative));
    }
    public AgentStation(System.Windows.Point pos) : base(pos) {
        Color = Colors.SandyBrown;
        bitmapImage = new BitmapImage(new Uri(@"../../../Model/Map/Stations/garage.png", UriKind.Relative));
    }
    #endregion

    #region Misc
    public void Assign(Agent agent) {
        if (AssignedAgents.Contains(agent)) return;
        var a = AssignedAgents.ToList();
        a.Add(agent);
        AssignedAgents = a.ToArray();
    }
    public void Remove(Agent agent) {
        var a = AssignedAgents.ToList();
        a.Remove(agent);
        AssignedAgents = a.ToArray();
    }
    public void Assign(Road road) {
        var a = AssignedRoads.ToList();
        a.Add(road);
        AssignedRoads = a.ToArray();
        road.ReservedStation = this;
    }
    public void Remove(Road road) {
        var a = AssignedRoads.ToList();
        a.Remove(road);
        AssignedRoads = a.ToArray();
        road.ReservedStation = null;
    }
    #endregion

    #region Control
    public void Distribute() {
        for (int i = 0; i < AssignedRoads.Length; i++) {
            if (!FreeAgents.Any()) return;
            for (int j = 0; j < FreeAgents.Length; j++) {
                Link(FreeAgents[j], AssignedRoads[i]);
            }
        }
    }

    public void Link(Agent agent, Road road) {
        road.ReservedAgent = agent;
        agent.AttachedObj = road;
        agent.TargetPosition = agent.Position ^ road;
    }
    public void Free(Agent agent) {
        if (agent.AttachedObj is null) return;
        agent.AttachedObj.ReservedAgent = null;
        agent.AttachedObj = null;
        agent.CurrentState = RobotState.Ready;
    }
    private void Refuel(Agent agent, TacticalMap map) {
        if ((agent.Position - Position).Length < 10) {
            agent.Fuel++;
        } else {
            Free(agent);
            Station st = map.Stations.Where(p => p is IRefueller r).OrderBy(p => PathFinder.Distance(p.Position, agent.Position)).First();
            agent.TargetPosition = Position;
        }
    }
    public void ChangeDevice(SnowRemover agent, SnowRemoverType type) {
        var d = agent.Devices.ToList();
        d.RemoveAll(p => type < SnowRemoverType.Cleaver ? p < SnowRemoverType.Cleaver : p >= SnowRemoverType.Cleaver);
        d.Add(type);
        agent.Devices = d.ToArray();
    }
    #endregion
}
