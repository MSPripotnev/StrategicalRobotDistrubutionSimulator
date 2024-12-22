namespace SRDS.Direct.Strategical;
using Agents;
using Agents.Drones;

using Executive;

using Model.Map;
using Model.Map.Stations;

public enum ActionRecommendation {
    Approve,
    Delay,
    IncreasePower
}

public class StrategicQualifier {
    public double SnownessDelayThreshold { get; set; }
    public double SnownessIncreasePowerThreshold { get; set; }
    public double IcyDelayThreshold { get; set; }
    public double IcyIncreasePowerThreshold { get; set; }
    public double DistanceThreshold { get; set; }
    public StrategicQualifier(double snowDelay, double snowIncrease, double icyDelay, double icyIncrease, double distance = 10.0) {
        SnownessDelayThreshold = snowDelay;
        SnownessIncreasePowerThreshold = snowIncrease;
        IcyDelayThreshold = icyDelay;
        IcyIncreasePowerThreshold = icyIncrease;
        DistanceThreshold = distance;
    }
    public ActionRecommendation Recommend(ActionType type, ActionResult expected, ActionResult? real) {
        switch (type) {
        case ActionType.GoTo: {
            if (expected.SubjectAfter is not Agent agent || real?.SubjectAfter is not Agent realAgent) throw new InvalidCastException();

            if (PathFinder.Distance(agent.Position, realAgent.Position) < DistanceThreshold)
                return ActionRecommendation.Approve;
            return ActionRecommendation.Delay;
        }
        case ActionType.WorkOn: {
            if (expected.SubjectAfter is not Agent agent) throw new InvalidCastException();

            if (real?.ObjectAfter is AgentStation station) {
                if (station.AssignedAgents.Contains(agent))
                    return ActionRecommendation.Approve;
                else return ActionRecommendation.Delay;
            }
            if (expected.ObjectAfter is Road roadExpected && real?.ObjectAfter is Road roadReal && agent is SnowRemover remover) {
                if (remover.Devices.Contains(SnowRemoverType.Shovel) || remover.Devices.Contains(SnowRemoverType.Rotor) || remover.Devices.Contains(SnowRemoverType.PlowBrush)) {
                    if (roadReal.Snowness - roadExpected.Snowness < SnownessDelayThreshold)
                        return ActionRecommendation.Approve;
                    if (roadReal.Snowness - roadExpected.Snowness < SnownessIncreasePowerThreshold)
                        return ActionRecommendation.Delay;
                    return ActionRecommendation.IncreasePower;
                }
                if (remover.Devices.Contains(SnowRemoverType.Cleaver) || remover.Devices.Contains(SnowRemoverType.AntiIceDistributor)) {
                    if (roadReal.IcyPercent - roadExpected.IcyPercent < IcyDelayThreshold)
                        return ActionRecommendation.Approve;
                    if (roadReal.IcyPercent - roadExpected.IcyPercent < IcyIncreasePowerThreshold)
                        return ActionRecommendation.Delay;
                    return ActionRecommendation.IncreasePower;
                }
            }

            if (expected.ObjectAfter is null) return ActionRecommendation.Approve;
            return ActionRecommendation.Delay;
        }
        case ActionType.ChangeDevice: {
            if (real?.SubjectAfter is not SnowRemover remover || real?.ObjectAfter is not SnowRemoverType device) throw new InvalidCastException();
            if (remover.Devices.Contains(device))
                return ActionRecommendation.Approve;
            return ActionRecommendation.Delay;
        }
        case ActionType.Refuel: {
            if (real?.SubjectAfter is not Agent agentReal || expected.SubjectAfter is not Agent agentExpected) throw new InvalidCastException();
            if (agentReal.Fuel >= agentExpected.Fuel)
                return ActionRecommendation.Approve;
            return ActionRecommendation.Delay;
        }
        default: throw new ArgumentException($"Action type '{type}' not found");
        }
    }
    public ActionResult Qualify(Director director, SystemAction action, DateTime time) {
        IControllable? subject;
        {
            if (action.Subject is AgentStation station)
                subject = director.Map.Stations.OfType<AgentStation>().First(p => p == station);
            else if (action.Subject is Agent agent)
                subject = director.Agents.First(p => p == agent);
            else subject = action.Subject;
        }

        object? _object = null;
        {
            if (action.Object is AgentStation station)
                _object = director.Map.Stations.OfType<AgentStation>().First(p => p == station);
            else if (action.Object is Road road)
                _object = director.Map.Roads.First(p => p == road);
            else _object = action.Object;
        }

       return new ActionResult() {
            SubjectAfter = subject,
            ObjectAfter = _object,
            EstimatedTime = time - action.StartTime
        };
    }
}
