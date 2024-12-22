using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace SRDS.Model.Map.Stations;
using Direct;
using Direct.Agents;
using Direct.Executive;

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

    public void Simulate(object? sender, DateTime time) {
        for (int i = 0; i < AssignedAgents.Length; i++)
            do
                AssignedAgents[i].Simulate(sender is Director ? this : sender, time);
            while (AssignedAgents[i].CurrentState == RobotState.Thinking);
    }

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
    public bool Assign(Agent agent) {
        if (AssignedAgents.Contains(agent)) return true;
        if (PathFinder.Distance(agent.Position, Position) < 10) return false;
        var a = AssignedAgents.ToList();
        a.Add(agent);
        AssignedAgents = a.ToArray();
        return true;
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

    public override int GetHashCode() => base.GetHashCode();
    public override bool Equals(object? obj) => base.Equals(obj);
    public static bool operator ==(AgentStation? a, AgentStation? b) {
        return (a is null && b is null) || a is not null && b is not null && PathFinder.Distance(a.Position, b.Position) < 15 && a.AssignedAgents.All(p => b.AssignedAgents.Contains(p)) && a.AssignedRoads.All(p => b.AssignedRoads.Contains(p));
    }
    public static bool operator !=(AgentStation? a, AgentStation? b) {
        return !(a == b);
    }
}
