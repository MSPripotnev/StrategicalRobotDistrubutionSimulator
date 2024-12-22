using System.Windows;

namespace SRDS.Direct.Strategical;
using Agents;
using Agents.Drones;

using Executive.Explorers.AStar;

using Model.Map;
using Model.Map.Stations;

public class Planner {
    public static (SystemAction goAction, SystemAction action)? RefuelPlan(Agent agent, TacticalMap map, DateTime time, double? fuel = null) {
        Station? station = FindNearestRefuelStation(agent, map);
        if (station is null) return null;

        var goAction = GoToPlan(agent, station.Position, time);
        if (goAction is null) return null;
        var subjectConstructorInfo = agent.GetType().GetConstructor(new Type[] { agent.GetType(), typeof(RobotState) }) ?? throw new InvalidCastException();
        var subject = (Agent)subjectConstructorInfo.Invoke(new object?[] { agent, RobotState.Ready });
        subject.Fuel = fuel ?? subject.FuelCapacity;

        ActionResult result = new ActionResult() { SubjectAfter = subject, };
        var action = new SystemAction(goAction.EndTime, goAction.EndTime, ActionType.Refuel, agent, station) { ExpectedResult = result };
        goAction.Next = action;

        return (goAction, action);
    }
    public static (SystemAction goAction, SystemAction action)? ChangeDevicePlan(SnowRemover agent, AgentStation station, SnowRemoverType device, DateTime time) {
        var resultAgent = new SnowRemover(agent);
        resultAgent.ChangeDevice(device);
        var goAction = GoToPlan(agent, station.Position, time);
        if (goAction is null) return null;
        var action = new SystemAction(goAction.EndTime, goAction.EndTime, ActionType.ChangeDevice, agent, device) {
            ExpectedResult = new ActionResult() { SubjectAfter = new SnowRemover(agent) },
        };
        goAction.Next = action;

        return (goAction, action);
    }
    public static (SystemAction goAction, SystemAction workAction, SystemAction? returnAction)? WorkOnRoad(SnowRemover agent, Road road, DateTime startTime, DateTime endTime, double snowness, double icy) {
        var goAction = GoToPlan(agent, agent.Position ^ road, startTime);
        if (goAction is null) return null;

        SystemAction? returnAction = null;
        if (endTime < DateTime.MaxValue) {
            returnAction = GoToPlan(agent, agent.Home is not null ? agent.Home.Position : agent.Position, endTime);
            if (returnAction is null) return null;
        }

        var action = new SystemAction(goAction.EndTime, DateTime.MaxValue, ActionType.WorkOn, agent, road) {
            ExpectedResult = new ActionResult() {
                SubjectAfter = new SnowRemover(agent, RobotState.Working) { Position = agent.Position ^ road },
                ObjectAfter = new Road(road) {
                    Snowness = snowness,
                    IcyPercent = icy
                },
                EstimatedTime = (returnAction is not null ? returnAction.EndTime : endTime) - startTime
            }
        };
        goAction.Next = action;
        action.Next = returnAction;

        if (action is null) return null;
        return (goAction, action, returnAction);
    }
    public static SystemAction? GoToPlan(Agent agent, Point targetPosition, DateTime time) {
        if (agent.Pathfinder is null) return null;
        AStarExplorer explorer = new AStarExplorer(agent.Position, targetPosition, agent.Pathfinder.Scale, agent.Pathfinder.Map, agent.InteractDistance);
        if (!explorer.FindWaySync())
            return null;

        var subjectConstructorInfo = agent.GetType().GetConstructor(new Type[] { agent.GetType(), typeof(RobotState) }) ?? throw new InvalidCastException();

        ActionResult result = new ActionResult() {
            SubjectAfter = (IControllable)subjectConstructorInfo.Invoke(new object?[] { agent, null }),
            EstimatedTime = time.AddMinutes(explorer.Result.Distance / agent.Speed) - time
        };
        if (result.SubjectAfter is not Agent ragent) return null;
        ragent.Position = explorer.Result;
        ragent.CurrentState = RobotState.Ready;

        return new SystemAction(time, time.AddMinutes(explorer.Result.Distance > 10 ? explorer.Result.Distance / agent.Speed / 60.0 : 0.0), ActionType.GoTo, result, agent, targetPosition);
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
