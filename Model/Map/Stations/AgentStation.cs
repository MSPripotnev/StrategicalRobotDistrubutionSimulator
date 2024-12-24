using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace SRDS.Model.Map.Stations;
using Direct;
using Direct.Agents;
using Direct.Executive;

using PropertyTools.DataAnnotations;

using SRDS.Direct.Agents.Drones;
using SRDS.Direct.Strategical;
using SRDS.Direct.Tactical.Qualifiers;

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
    [Browsable(false)]
    public Agent[] FreeAgents {
        get {
            return AssignedAgents.Where(x => x.CurrentState == RobotState.Ready).ToArray();
        }
    }
    public IQualifier DistributorQualifier { get; set; }
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

        Dictionary<string, (double min, double max)> values = new Dictionary<string, (double min, double max)>() {
            { "DistanceToTarget", (50, 400) },
            { nameof(Agent.Fuel), (0, 100) },
            { nameof(Road.Length), (0, 800) },
            { nameof(Road.Snowness), (0, 100) },
            { nameof(Road.IcyPercent), (0, 100)},
            { nameof(SnowRemover.RemoveSpeed), (0.5, 1.0)},
            { nameof(SnowRemover.MashSpeed), (1.0, 2.0)}
        };
        DistributorQualifier = new FuzzyQualifier(values);
    }
    public AgentStation(System.Windows.Point pos) : this() {
        Position = pos;
    }
    #endregion

    #region Operations
    public SystemAction[] FormLocalWorkPlan(SnowRemover[] selectedAgentsForRoad, Road road, DateTime startTime, DateTime endTime) {
        if (!AssignedRoads.Contains(road)) return Array.Empty<SystemAction>();
        List<SystemAction> actions = new();
        for (int i = 0; i < selectedAgentsForRoad.Length; i++) {
            TimeSpan followTime = new TimeSpan(0, i * 5, 0);
            var action = Planner.WorkOnRoad(selectedAgentsForRoad[i], road, startTime + followTime * i, endTime - followTime * i);
            if (!action.HasValue) continue;
            actions.Add(action.Value.goAction);
        }
        return actions.ToArray();
    }
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

    public bool Distribute(int nAgents, int nRoads) {
        if (nAgents > AssignedAgents.Length || nRoads > AssignedRoads.Length) return false;
        var roads = AssignedRoads.OrderBy(p => p.Length).Take(nRoads).ToList();
        double roadsSumLength = AssignedRoads.Sum(p => p.Length);
        var roadsRelevance = AssignedRoads.Select(p => p.Length / roadsSumLength);
        Dictionary<Road, double> agentsForRoads = new Dictionary<Road, int>();
        for (int i = 0; i < AssignedRoads.Length; i++) {
            double agentsForRoad = 2 * AssignedRoads[i].Length * (6 - AssignedRoads[i].Category) / 50 / 0.8 / 6;
            agentsForRoads.Add(AssignedRoads[i], );
        }
        var agents = AssignedAgents.OrderBy(p => p).Take(nAgents).ToList();
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
