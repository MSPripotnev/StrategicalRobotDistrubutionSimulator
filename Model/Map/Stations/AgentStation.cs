using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace SRDS.Model.Map.Stations;

using System.ComponentModel;

using Direct;
using Direct.Agents;
using Direct.Agents.Drones;
using Direct.ControlSystem;
using Direct.Executive;
using Direct.Strategical;

public enum SystemState {

}

public enum FuzzySystemState {

}

public class AgentStation : Station, IControllable, IRefueller {
    public new event PropertyChangedEventHandler? PropertyChanged;
    #region Properties
    [XmlIgnore]
    public Agent[] AssignedAgents { get; set; } = Array.Empty<Agent>();
    [XmlIgnore]
    public Road[] AssignedRoads { get; set; } = Array.Empty<Road>();
    [XmlIgnore]
    [PropertyTools.DataAnnotations.Browsable(false)]
    public Agent[] FreeAgents {
        get {
            return AssignedAgents.Where(x => x.CurrentState == RobotState.Ready).ToArray();
        }
    }
    [XmlIgnore]
    public SystemAction[] LocalPlans { get; set; } = Array.Empty<SystemAction>();
    [XmlIgnore]
    public ExpertSnowRemovePlanner PlannerModule { get; set; } = new();
    #endregion

    public void Simulate(object? sender, DateTime time) {
        for (int i = 0; i < AssignedAgents.Length; i++)
            do
                AssignedAgents[i].Simulate(sender is Director ? this : sender, time);
            while (AssignedAgents[i].CurrentState == RobotState.Thinking);

        if (sender is not Director director) return;

        // TODO: replace new plan by plans correction
        if (time.Second == 0 && time.Minute % 10 == 0) {
            var hasUnfinishedPlans = LocalPlans.Any(p => p.Descendants().Any(p => !p.Finished));
            if (!hasUnfinishedPlans) {
                LocalPlans = PlannerModule.PlanPrepare(this, director.Map, time, PlannerModule.Strength > 0);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalPlans)));
            }
            PlannerModule.Simulate(sender, time);
        }
    }

    #region Constructors
    public AgentStation() : base() {
        Color = Colors.SandyBrown;
        bitmapImage = new BitmapImage(new Uri(@"../../../Model/Map/Stations/garage.png", UriKind.Relative));
    }
    public AgentStation(System.Windows.Point pos) : this() {
        Position = pos;
    }
    #endregion

    #region Operations
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
    public bool Assign(Road road) {
        if (AssignedRoads.Contains(road)) return true;
        var a = AssignedRoads.ToList();
        a.Add(road);
        AssignedRoads = a.ToArray();
        road.ReservedStation = this;
        return true;
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
