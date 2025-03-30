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
    public static (SystemAction goAction, SystemAction action)? RefuelPlan(Agent agent, TacticalMap map, DateTime start, DateTime? end = null, double? fuel = null, double? deicing = null) {
        Station? station = FindNearestRefuelStation(agent, map);
        if (station is null) return null;

        var goAction = GoToPlan(agent, station.Position, start);
        if (goAction is null) return null;

        var subjectConstructorInfo = agent.GetType().GetConstructor(new Type[] { agent.GetType(), typeof(RobotState) }) ?? throw new InvalidCastException();
        var subject = (Agent)subjectConstructorInfo.Invoke(new object?[] { agent, RobotState.Ready });
        subject.Fuel = fuel ?? subject.FuelCapacity;

        TimeSpan refuelTime = new TimeSpan(0, 0, (int)Math.Ceiling((subject.Fuel - agent.Fuel) / Agent.FuelIncrease));
        if (subject is SnowRemover remover && remover.Devices.FirstOrDefault(p => p?.Type == SnowRemoverType.AntiIceDistributor, null) is SnowRemoveDevice antiIceDevice) {
            var antiIceFuelTime = new TimeSpan(0, 0, (int)Math.Ceiling((antiIceDevice.DeicingCapacity - antiIceDevice.DeicingCurrent) / Agent.FuelIncrease));
            refuelTime = refuelTime.TotalSeconds > antiIceFuelTime.TotalSeconds ? refuelTime : antiIceFuelTime;
            antiIceDevice.DeicingCurrent = deicing ?? antiIceDevice.DeicingCapacity;
        }
        if (end is DateTime endd && refuelTime > end - goAction.EndTime) {
            subject.Fuel = (endd - goAction.EndTime).TotalSeconds * Agent.FuelIncrease;
            if (agent is SnowRemover r && r.Devices.FirstOrDefault(p => p?.Type == SnowRemoverType.AntiIceDistributor, null) is SnowRemoveDevice antiIceDevice2)
                antiIceDevice2.DeicingCurrent = (endd - goAction.EndTime).TotalSeconds * Agent.FuelIncrease;
            refuelTime = endd - goAction.EndTime;
        }
        if (refuelTime <= TimeSpan.Zero)
            return null;

        ActionResult result = new ActionResult() {
            SubjectAfter = subject,
            EstimatedTime = refuelTime
        };
        var action = new SystemAction(goAction.EndTime, goAction.EndTime + refuelTime, ActionType.Refuel, result, agent, station);
        goAction.Next.Add(action);

        return (goAction, action);
    }
    public static (SystemAction goAction, SystemAction action)? ChangeDevicePlan(SnowRemover agent, AgentStation station, SnowRemoveDevice device, DateTime time) {
        var resultAgent = new SnowRemover(agent, RobotState.Refuel);
        resultAgent.ChangeDevice(device);
        var goAction = GoToPlan(resultAgent, station.Position, time);
        if (goAction is null) return null;
        var result = new ActionResult() {
            SubjectAfter = new SnowRemover(resultAgent, RobotState.Ready),
            EstimatedTime = time.AddMinutes(1) - time
        };
        var action = new SystemAction(goAction.EndTime, goAction.EndTime, ActionType.ChangeDevice, result, agent, device);
        goAction.Next.Add(action);

        return (goAction, action);
    }
    public static (SystemAction goAction, SystemAction workAction, SystemAction? returnAction)? WorkOnRoad(SnowRemover agent, Road road, DateTime startTime, DateTime workEndTime, double snowness = -1, double icy = -1) {
        var roadPosition = agent.Position ^ road;
        if (agent.Pathfinder is not null) {
            AStarExplorer explorer = new AStarExplorer(agent.Position, roadPosition, agent.Pathfinder.Scale, agent.Pathfinder.Map, agent.InteractDistance);
            if (explorer.FindWaySync()) {
                var path = PathFinder.CreatePathFromLastPoint(explorer.Result);
                var p = path?.FirstOrDefault(i => Math.Abs(road.DistanceToRoad(i)) < road.Height / 2, roadPosition);
                if (p.HasValue && p.Value.X != 0 && p.Value.Y != 0)
                    roadPosition = p.Value;
            }
        }

        Point roadNearestEndPoint;
        Vector v;
        if (PathFinder.Distance(road.Position, agent.Position) < PathFinder.Distance(road.EndPosition, agent.Position)) {
            v = road.EndPosition - road.Position;
            roadNearestEndPoint = road.Position;
        } else {
            v = road.Position - road.EndPosition;
            roadNearestEndPoint = road.EndPosition;
        }
        v *= road.Height / v.Length;
        (v.X, v.Y) = (-v.Y, v.X);
        if (PathFinder.Distance(roadPosition - v, roadNearestEndPoint) < PathFinder.Distance(roadPosition, roadNearestEndPoint))
            roadPosition -= v;

        (roadPosition.X, roadPosition.Y) = (Math.Round(roadPosition.X), Math.Round(roadPosition.Y));
        var goAction = GoToPlan(agent, roadPosition, startTime);
        if (goAction is null) return null;

        SystemAction? returnAction = null;
        var workResult = new ActionResult() {
            SubjectAfter = new SnowRemover(agent, RobotState.Working) { Position = roadPosition },
            ObjectAfter = new Road(road) {
                Snowness = snowness,
                IcyPercent = icy
            },
            EstimatedTime = workEndTime - startTime
        };
        var action = new SystemAction(goAction.EndTime, workEndTime, ActionType.WorkOn, workResult, agent, road);
        goAction.Next.Add(action);

        if (workEndTime < DateTime.MaxValue) {
            returnAction = GoToPlan(new SnowRemover(workResult.SubjectAfter as SnowRemover ?? throw new Exception()), agent.Home is not null ? agent.Home.Position : agent.Position, workEndTime);
            if (returnAction is null) return null;
            action.Next.Add(returnAction);
        }

        if (action is null) return null;
        return (goAction, action, returnAction);
    }
    public static SystemAction? GoToPlan(Agent agent, Point targetPosition, DateTime startTime) {
        if (agent.Pathfinder is null) return null;
        AStarExplorer explorer = new AStarExplorer(agent.Position, targetPosition, agent.Pathfinder.Scale, agent.Pathfinder.Map, agent.InteractDistance);
        if (!explorer.FindWaySync())
            return null;

        var subjectConstructorInfo = agent.GetType().GetConstructor(new Type[] { agent.GetType(), typeof(RobotState) }) ?? throw new InvalidCastException();

        ActionResult result = new ActionResult() {
            SubjectAfter = (IControllable)subjectConstructorInfo.Invoke(new object?[] { agent, null }),
            EstimatedTime = startTime.AddSeconds(Math.Round(explorer.Result.Distance / agent.Speed)) - startTime
        };
        if (result.SubjectAfter is not Agent ragent) return null;
        ragent.Position = explorer.Result;

        return new SystemAction(startTime, startTime.AddSeconds(explorer.Result.Distance > 10 ? Math.Round(explorer.Result.Distance / agent.Speed) : 0.0), ActionType.GoTo, result, agent, targetPosition);
    }
    private static Station? FindNearestRefuelStation(Agent agent, TacticalMap map) {
        var refuelStations = map.Stations.Where(p => p is GasStation or AgentStation).ToArray();
        if (refuelStations is null || !refuelStations.Any()) return null;

        List<double> distances = new() { double.MaxValue, };
        for (int i = 0; i < refuelStations.Length; i++) {
            AStarExplorer explorer = new AStarExplorer(agent.Position, refuelStations[i].Position, agent.Pathfinder?.Scale ?? 0, agent.Pathfinder?.Map ?? throw new InvalidOperationException(), agent.InteractDistance);
            if (!explorer.FindWaySync()) continue;
            distances[i] = explorer.Result.Distance;
        }
        return refuelStations[distances.FindIndex(p => p == distances.Min())];
    }
}
