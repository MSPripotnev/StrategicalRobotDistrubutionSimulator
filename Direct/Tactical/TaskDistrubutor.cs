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

public class TaskDistributor
{

    #region Properties
    public Agent[]? Agents { private get; set; }
    public Target[]? Targets { private get; set; }
    public TacticalMap Map { private get; set; }
    [XmlIgnore]
    public IQualifier Qualifier { get; set; }
    public Agent[] NonAssignedAgents
    {
        get => Agents is not null ? Agents.Where(p => p.Home == null).ToArray() : Array.Empty<Agent>();
    }
    [XmlIgnore]
    public Agent[] FreeAgents
    {
        get => Agents is not null ? Agents.Where(x => x.CurrentState == RobotState.Ready).ToArray() : Array.Empty<Agent>();
    }
    public Target[] FreeTargets
    {
        get => Targets is not null ? Targets.Where(x => x.ReservedAgent == null && !x.Finished).ToArray() : Array.Empty<Target>();
    }
    [XmlIgnore]
    public Dictionary<Target, DistributionQualifyReading> DistributionQualifyReadings { get; set; } = new();
    #endregion

    public TaskDistributor() : this(null) { }
    public TaskDistributor(Type? qualifyType)
    {
        if (qualifyType == null)
            qualifyType = typeof(DistanceQualifier);
        if (qualifyType?.GetConstructor(Type.EmptyTypes)?.Invoke(null) is IQualifier q)
            Qualifier = q;
        else throw new Exception($"Could not find constructor for type '{qualifyType?.FullName}'");
    }

    #region Distribution

    #region General
    public void DistributeTask(PropertyChangedEventHandler? propertyChanged)
    {
        DistributeTaskForFreeAgents(propertyChanged);
        DistributeTaskForWorkingTransporters();
    }

    private void DistributeTaskForFreeAgents(PropertyChangedEventHandler? propertyChanged)
    {
        var freeAgents = new List<Agent>(FreeAgents).Where(p => p.AttachedObj == null).ToArray();
        if (freeAgents.Length > 0)
        {
            CalculateTrajectoryForFreeAgents(ref freeAgents);
            FindNearestAgentWithTrajectoryForTarget();
        }
        propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FreeTargets)));
        propertyChanged?.Invoke(this, new PropertyChangedEventArgs("CollectedTargets"));
    }
    private void CalculateTrajectoryForFreeAgents(ref Agent[] freeAgents)
    {
        //распределение ближайших целей по роботам
        for (int i = 0; i < freeAgents.Length; i++)
        {
            Agent agent = freeAgents[i];
            Target? targetPos = Qualifier.RecommendTargetForAgent(agent, FreeTargets.Where(p => !agent.BlockedTargets.Contains(p)));
            if (targetPos == null)
                continue;

            LinkTargetToAgent(agent, targetPos);

            if (!agent.Trajectory.Any())
            {
                agent.AttachedObj = null;
                agent.BlockedTargets.Add(targetPos);
            }
        }
    }
    private void FindNearestAgentWithTrajectoryForTarget()
    {
        for (int i = 0; i < FreeTargets.Length; i++)
        {
            Target t = FreeTargets[i];
            var AttachedAgents = Agents?.Where(p => p.AttachedObj == t &&
                    (p.CurrentState == RobotState.Ready || p.CurrentState == RobotState.Thinking))
                .ToArray();

            if (AttachedAgents?.Length > 0)
            {
                t.ReservedAgent = AttachedAgents.MaxBy(p => Qualifier.Qualify(p, t));
                for (int j = 0; j < AttachedAgents.Length; j++)
                {
                    if (AttachedAgents[j] != t.ReservedAgent)
                        UnlinkTargetFromAgent(AttachedAgents[j]);
                }
                RecordDistribution(t, Map.Path);
            }
        }
    }
    #endregion

    #region Specific
    private void DistributeTaskForWorkingTransporters()
    {
        var WorkingTransporters = Agents?.Where(
            p => p is Transporter && p.CurrentState == RobotState.Working && Map.Stations
            .Where(p => p is CollectingStation)
            .All(b => PathFinder.Distance(b.Position, p.TargetPosition) > p.InteractDistance))
            .ToList();

        if (WorkingTransporters?.Count > 0)
        {
            for (int i = 0; i < WorkingTransporters.Count; i++)
            {
                Transporter transporter = (Transporter)WorkingTransporters[i];
                CollectingStation? nearBase = (CollectingStation?)Map.Stations.Where(p => p is CollectingStation).MinBy(p => PathFinder.Distance(p.Position, transporter.Position));

                if (nearBase == null)
                    return;

                if ((nearBase.Position - transporter.BackTrajectory[^1]).Length < transporter.InteractDistance / 2)
                {
                    transporter.Trajectory = transporter.BackTrajectory.ToList();
                    if (transporter.Trajectory[^1] != nearBase.Position)
                        transporter.Trajectory[^1] = nearBase.Position;
                    transporter.BackTrajectory = null;
                    transporter.AttachedObj.ReservedAgent = transporter;
                }
                else if (PathFinder.Distance(transporter.TargetPosition, nearBase.Position) > transporter.InteractDistance)
                {
                    transporter.TargetPosition = nearBase.Position;
                    transporter.AttachedObj.ReservedAgent = transporter;
                }
                else
                {
                    transporter.CurrentState = RobotState.Ready;
                }
            }
        }
    }
    #endregion

    #region Links
    private static void LinkTargetToAgent(Agent agent, Target target)
    {
        if (target == null)
            return;
        agent.AttachedObj = target;
        agent.TargetPosition = target.Position;
    }
    private static void UnlinkTargetFromAgent(Agent agent)
    {
        agent.AttachedObj = null;
        agent.Trajectory.Clear();
        agent.CurrentState = RobotState.Ready;
    }
    #endregion

    #endregion

    #region Record
    public void RecordDistribution(Target t, string mapPath)
    {
        if (Qualifier is FuzzyQualifier f)
        {
            f.Qualify(t.ReservedAgent, t, out var rs);
            DistributionQualifyReadings.Add(t, new()
            {
                ModelName = mapPath,
                Rules = rs,
                TakedTarget = t,
                AgentPosition = t.ReservedAgent.Position,
                TakedLevel = t is Snowdrift s ? s.Level : 0,
            });
        }
        else
        {
            DistributionQualifyReadings.Add(t, new()
            {
                ModelName = mapPath,
                TakedTarget = t,
                AgentPosition = t.ReservedAgent.Position,
                TakedLevel = t is Snowdrift s ? s.Level : 0,
            });
        }
    }

    public void UpdateDistribution(Agent agent)
    {
        if (!DistributionQualifyReadings.ContainsKey(agent.AttachedObj))
            return;
        if (agent.CurrentState == RobotState.Working)
            DistributionQualifyReadings[agent.AttachedObj].WorkingTime++;
        else if (agent.CurrentState == RobotState.Going)
            DistributionQualifyReadings[agent.AttachedObj].WayTime++;
        DistributionQualifyReadings[agent.AttachedObj].FuelCost += Agent.FuelDecrease;
    }
    #endregion
}
