using System.Windows;

namespace SRDS.Direct.Strategical;
using Agents;
using Agents.Drones;

using Model.Map;
using Model.Map.Stations;

using Executive;
using Tactical;
using Tactical.Explorers.AStar;

public class Planner {

    public static SystemAction DistributePlan(AgentStation ags, Dictionary<Road, double> distributionRecommendation, DateTime start, DateTime end) {
        return new SystemAction(start, end, ActionType.WorkOn, ags, distributionRecommendation);
    }
    public static SystemAction? RefuelPlan(Agent agent, TacticalMap map, DateTime start, DateTime? end = null, double? fuel = null, double? deicing = null) {
        Station? station = FindNearestRefuelStation(agent, map);
        if (station is null) return null;

        var subjectConstructorInfo = agent.GetType().GetConstructor(new Type[] { agent.GetType(), typeof(RobotState) }) ?? throw new InvalidCastException();
        var subject = (Agent)subjectConstructorInfo.Invoke(new object?[] { agent, RobotState.Ready });
        subject.Fuel = fuel ?? subject.FuelCapacity;

        TimeSpan refuelTime = new TimeSpan(0, 0, (int)Math.Ceiling((subject.Fuel - agent.Fuel) / Agent.FuelIncrease));
        if (subject is SnowRemover remover && remover.Devices.FirstOrDefault(p => p?.Type == SnowRemoverType.AntiIceDistributor, null) is SnowRemoveDevice antiIceDevice) {
            var antiIceFuelTime = new TimeSpan(0, 0, (int)Math.Ceiling((antiIceDevice.DeicingCapacity - antiIceDevice.DeicingCurrent) / Agent.FuelIncrease));
            refuelTime = refuelTime.TotalSeconds > antiIceFuelTime.TotalSeconds ? refuelTime : antiIceFuelTime;
            antiIceDevice.DeicingCurrent = deicing ?? antiIceDevice.DeicingCapacity;
        }
        if (end is DateTime endd && refuelTime > end - start) {
            subject.Fuel = (endd - start).TotalSeconds * Agent.FuelIncrease;
            if (agent is SnowRemover r && r.Devices.FirstOrDefault(p => p?.Type == SnowRemoverType.AntiIceDistributor, null) is SnowRemoveDevice antiIceDevice2)
                antiIceDevice2.DeicingCurrent = (endd - start).TotalSeconds * Agent.FuelIncrease;
            refuelTime = endd - start;
        }
        if (refuelTime <= TimeSpan.Zero)
            return null;

        ActionResult result = new ActionResult() {
            SubjectAfter = subject,
            EstimatedTime = refuelTime
        };
        var action = new SystemAction(start, start + refuelTime, ActionType.Refuel, result, agent, station);

        return action;
    }
    public static SystemAction? ChangeDevicePlan(SnowRemover agent, SnowRemoveDevice device, DateTime time) {
        var resultAgent = new SnowRemover(agent, RobotState.Refuel);
        resultAgent.ChangeDevice(device);
        var result = new ActionResult() {
            SubjectAfter = new SnowRemover(resultAgent, RobotState.Ready),
            EstimatedTime = time.AddMinutes(1) - time
        };
        var action = new SystemAction(time, time + result.EstimatedTime, ActionType.ChangeDevice, result, agent, device);

        return action;
    }
    public static SystemAction? WorkOnRoad(SnowRemover agent, Road road, DateTime startTime, DateTime workEndTime, double snowness = -1, double icy = -1) {
        var workResult = new ActionResult() {
            SubjectAfter = new SnowRemover(agent, RobotState.Working) { Position = agent.Position ^ road },
            ObjectAfter = new Road(road) {
                Snowness = snowness,
                IcyPercent = icy
            },
            EstimatedTime = workEndTime - startTime
        };
        var action = new SystemAction(startTime, workEndTime, ActionType.WorkOn, workResult, agent, road);

        if (action is null) return null;
        return action;
    }
    public static Station? FindNearestRefuelStation(Agent agent, TacticalMap map) {
        var refuelStations = map.Stations.Where(p => p is GasStation or AgentStation).ToArray();
        if (refuelStations is null || !refuelStations.Any()) return null;

        List<double> distances = new();
        for (int i = 0; i < refuelStations.Length; i++) {
            AStarExplorer explorer = new AStarExplorer(agent.Position, refuelStations[i].Position, agent.Pathfinder?.Scale ?? 0, agent.Pathfinder?.Map ?? throw new InvalidOperationException(), agent.InteractDistance);
            if (!explorer.FindWaySync()) continue;
            distances.Add(explorer.Result.Distance);
        }
        return refuelStations[distances.FindIndex(p => p == distances.Min())];
    }
}
