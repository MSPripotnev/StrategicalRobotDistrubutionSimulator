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
using SRDS.Direct.Tactical;

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
    [XmlIgnore]
    public SystemAction? CurrentAction { get; set; }
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
            var snownedRoads = director.Map.Roads.Any(p => p.Snowness > 0 ||
                    director.Map.Roads.Any(p => p.IcyPercent > 0));
            if (!hasUnfinishedPlans) {
                LocalPlans = PlannerModule.PlanPrepare(this, director.Map, time, snownedRoads && PlannerModule.Strength > 0);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalPlans)));
            }
            PlannerModule.Simulate(sender, time);
        }
    }

    public TaskNotExecutedReason? Execute(ref SystemAction action) {
        if (action.Type != ActionType.WorkOn)
            throw new NotImplementedException();

        if (action.Object is Agent n_agent)
            Assign(n_agent);
        else if (action.Object is Road n_road)
            Assign(n_road);
        else return TaskNotExecutedReason.Unknown;
        return TaskNotExecutedReason.AlreadyCompleted;
    }
    public bool Reaction(TaskNotExecutedReason? reason, SystemAction? action = null) {
        return true;
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
