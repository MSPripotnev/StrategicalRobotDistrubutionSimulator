using System.ComponentModel;
using System.Xml.Serialization;

namespace SRDS.Direct.Tactical;
using Agents;
using Agents.Drones;

using Model.Map;
using Model.Map.Stations;
using Model.Targets;
using SRDS.Direct.Tactical.Qualifiers;
using SRDS.Direct.Executive;
using System.Drawing;

public class TaskDistributor {

    #region Properties
    [XmlIgnore]
    public Agent[]? Agents { private get; set; }
    [XmlIgnore]
    public Target[]? Targets { private get; set; }
    [XmlIgnore]
    public TacticalMap Map { private get; set; }
    [XmlIgnore]
    public IQualifier Qualifier { get; set; }
    [XmlIgnore]
    public Agent[] NonAssignedAgents {
        get => Agents is not null ? Agents.Where(p => p.Home is null).ToArray() : Array.Empty<Agent>();
    }
    [XmlIgnore]
    public Agent[] FreeAgents {
        get => Agents is not null ? Agents.Where(x => x.CurrentState == RobotState.Ready).ToArray() : Array.Empty<Agent>();
    }
    [XmlIgnore]
    public Target[] FreeTargets {
        get => Targets is not null ? Targets.Where(x => x.ReservedAgent is null && !x.Finished).ToArray() : Array.Empty<Target>();
    }
    [XmlIgnore]
    public Dictionary<ITargetable, DistributionQualifyReading> DistributionQualifyReadings { get; set; } = new();
    #endregion

    public TaskDistributor() : this(null, new TacticalMap(), new Dictionary<string, (double min, double max)>()) { }
    public TaskDistributor(Type? qualifyType, TacticalMap map, Dictionary<string, (double min, double max)> inputVars) {
        if (qualifyType == null)
            qualifyType = typeof(DistanceQualifier);
        if (qualifyType == typeof(FuzzyQualifier) && qualifyType?.GetConstructor(new Type[] { typeof(Dictionary<string, (double min, double max)>) })?.Invoke(new object[] { inputVars }) is IQualifier q)
            Qualifier = q;
        else if (qualifyType == typeof(DistanceQualifier)) 
            Qualifier = new DistanceQualifier();
        else throw new Exception($"Could not find constructor for type '{qualifyType?.FullName}'");
        Map = map;
        DistributeRoadsBetweenStations();
    }

    #region Distribution

    public static Dictionary<string, (double min, double max)> GetSnowdriftControlFuzzyQualifyVariables() {
        SnowRemover r;
        Snowdrift s;
        return new Dictionary<string, (double min, double max)>() {
            { "DistanceToTarget", (50, 400) },
            { nameof(r.Fuel), (0, 100) },
            { nameof(s.Level), (0, 40) },
            { nameof(s.MashPercent), (0, 100)},
            { nameof(r.RemoveSpeed), (0.5, 1.0)},
            { nameof(r.MashSpeed), (1.0, 2.0)}
        };
    }

    #region General
    public void DistributeTask(PropertyChangedEventHandler? propertyChanged) {
        // DistributeTaskForFreeAgents(propertyChanged);
        DistributeTaskForWorkingTransporters();
        if (Map.Roads.Any(p => p.ReservedStation is null))
            DistributeRoadsBetweenStations();
    }

    private void DistributeTaskForFreeAgents(PropertyChangedEventHandler? propertyChanged) {
        var freeAgents = new List<Agent>(FreeAgents).Where(p => p.AttachedObj == null).ToArray();
        if (freeAgents.Length > 0) {
            CalculateTrajectoryForFreeAgents(ref freeAgents);
            FindNearestAgentWithTrajectoryForTarget();
        }
        propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FreeTargets)));
        propertyChanged?.Invoke(this, new PropertyChangedEventArgs("CollectedTargets"));
    }
    private void CalculateTrajectoryForFreeAgents(ref Agent[] freeAgents) {
        //распределение ближайших целей по роботам
        for (int i = 0; i < freeAgents.Length; i++) {
            Agent agent = freeAgents[i];
            ITargetable? target = Qualifier.RecommendTargetForAgent(agent, FreeTargets.Where(p => !agent.BlockedTargets.Contains(p)));
            if (target == null)
                continue;

            LinkTargetToAgent(agent, target);

            if (!agent.Trajectory.Any()) {
                agent.AttachedObj = null;
                agent.BlockedTargets.Add(target);
            }
        }
    }
    private void FindNearestAgentWithTrajectoryForTarget() {
        for (int i = 0; i < FreeTargets.Length; i++) {
            Target t = FreeTargets[i];
            var AttachedAgents = Agents?.Where(p => p.AttachedObj == t &&
                    (p.CurrentState == RobotState.Ready || p.CurrentState == RobotState.Thinking))
                .ToArray();

            if (AttachedAgents?.Length > 0) {
                t.ReservedAgent = AttachedAgents.MaxBy(p => Qualifier.Qualify(p, t));
                for (int j = 0; j < AttachedAgents.Length; j++) {
                    if (AttachedAgents[j] != t.ReservedAgent)
                        UnlinkTargetFromAgent(AttachedAgents[j]);
                }
                RecordDistribution(t, Map.Path);
            }
        }
    }
    #endregion

    #region Specific
    private void DistributeRoadsBetweenStations() {
        for (int i = 0; i < Map.Stations.Length; i++) {
            if (Map.Stations[i] is not AgentStation a) continue;
            if (a.AssignedRoads.Any()) {
                var ar = a.AssignedRoads.ToArray();
                for (int l = 0; l < ar.Length; l++)
                    a.Remove(ar[l]);
            }

            for (int j = 0; j < Map.Roads.Length; j++)
                a.Assign(Map.Roads[j]);
            return;
        }
    }

    private void DistributeTaskForWorkingTransporters() {
        var WorkingTransporters = Agents?.Where(
            p => p is Transporter && p.CurrentState == RobotState.Working && Map.Stations
            .Where(p => p is CollectingStation)
            .All(b => PathFinder.Distance(b.Position, p.TargetPosition) > p.InteractDistance))
            .ToList();

        if (WorkingTransporters?.Count > 0) {
            for (int i = 0; i < WorkingTransporters.Count; i++) {
                Transporter transporter = (Transporter)WorkingTransporters[i];
                CollectingStation? nearBase = (CollectingStation?)Map.Stations.Where(p => p is CollectingStation).MinBy(p => PathFinder.Distance(p.Position, transporter.Position));

                if (nearBase is null)
                    return;

                if ((nearBase.Position - transporter.BackTrajectory[^1]).Length < transporter.InteractDistance / 2) {
                    transporter.Trajectory = transporter.BackTrajectory.ToList();
                    if (transporter.Trajectory[^1] != nearBase.Position)
                        transporter.Trajectory[^1] = nearBase.Position;
                    transporter.BackTrajectory = Array.Empty<System.Windows.Point>();
                    if (transporter.AttachedObj is not null)
                        transporter.AttachedObj.ReservedAgent = transporter;
                } else if (PathFinder.Distance(transporter.TargetPosition, nearBase.Position) > transporter.InteractDistance) {
                    transporter.TargetPosition = nearBase.Position;
                    if (transporter.AttachedObj is not null)
                        transporter.AttachedObj.ReservedAgent = transporter;
                } else {
                    transporter.CurrentState = RobotState.Ready;
                }
            }
        }
    }
    #endregion

    #region Links
    private static void LinkTargetToAgent(Agent agent, ITargetable target) {
        if (target == null)
            return;
        agent.AttachedObj = target;
        agent.TargetPosition = target.Position;
    }
    private static void UnlinkTargetFromAgent(Agent agent) {
        agent.AttachedObj = null;
        agent.Trajectory.Clear();
        agent.CurrentState = RobotState.Ready;
    }
    #endregion

    #endregion

    #region Record
    public void RecordDistribution(Target t, string mapPath) {
        if (Qualifier is FuzzyQualifier f) {
            if (t.ReservedAgent is null) return;
            f.Qualify(t.ReservedAgent, t, out var rs);
            DistributionQualifyReadings.Add(t, new() {
                ModelName = mapPath,
                Rules = rs,
                TakedTarget = t,
                AgentPosition = t.ReservedAgent.Position,
                TakedLevel = t is Snowdrift s ? s.Level : 0,
            });
        } else {
            if (t.ReservedAgent is null) return;
            DistributionQualifyReadings.Add(t, new() {
                ModelName = mapPath,
                TakedTarget = t,
                AgentPosition = t.ReservedAgent.Position,
                TakedLevel = t is Snowdrift s ? s.Level : 0,
            });
        }
    }

    public void UpdateDistribution(Agent agent) {
        if (agent.AttachedObj is null || !DistributionQualifyReadings.ContainsKey(agent.AttachedObj))
            return;
        if (agent.CurrentState == RobotState.Working)
            DistributionQualifyReadings[agent.AttachedObj].WorkingTime++;
        else if (agent.CurrentState == RobotState.Going)
            DistributionQualifyReadings[agent.AttachedObj].WayTime++;
        DistributionQualifyReadings[agent.AttachedObj].FuelCost += Agent.FuelDecrease;
    }
    #endregion
}
