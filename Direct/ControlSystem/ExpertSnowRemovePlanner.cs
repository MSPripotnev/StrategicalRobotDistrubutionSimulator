namespace SRDS.Direct.ControlSystem;

using Direct.Agents.Drones;

using Model;
using Model.Map;
using Model.Map.Stations;

using SRDS.Direct.Agents;
using SRDS.Direct.Executive;

using Strategical;
public class ExpertSnowRemovePlanner : ITimeSimulatable {
    private int strength;
    public int Strength {
        get => strength;
        set {
            if (strength == value) return;
            strength = value;
        }
    }
    private int temperatureStrength;
    public int TemperatureStrength {
        get => temperatureStrength;
        set => temperatureStrength = value;
    }

    private void GetSnowfallStrength(Meteostation[] meteostations) {
        if (!meteostations.Any()) return;
        var fallSpeed = meteostations.Sum(p => p.PrecipitationIntensity) / meteostations.Length * 10;
        Strength = fallSpeed > 3 ? 3 : (fallSpeed >= 1 ? 2 : (fallSpeed >= 0.5 ? 1 : 0));
    }

    private void GetTemperatureStrength(Meteostation[] meteostations) {
        if (!meteostations.Any()) return;
        var temperature = meteostations.Sum(p => p.Temperature) / meteostations.Length;
        TemperatureStrength = temperature > -6 ? 2 : (temperature >= -18 ? 1 : 0);
        // TODO: increase deicing materials consumption
    }

    public static Dictionary<Road, double> DistributeAgentsCount(Road[] assignedRoads, TimeSpan snowremoversTimeInterval, double mapScale, int agentsCount = 0) {
        Dictionary<Road, double> agentsForRoads = new Dictionary<Road, double>();
        for (int i = 0; i < assignedRoads.Length; i++) {
            double agentsForRoad = 2 * assignedRoads[i].Length * mapScale / 1000.0 * (6 - assignedRoads[i].Category) / 90.0 / 0.8 / snowremoversTimeInterval.TotalHours;
            agentsForRoads.Add(assignedRoads[i], agentsForRoad);
        }
        int neededAgents = (int)(agentsForRoads.Sum(p => Math.Ceiling(p.Value)));
        if (agentsCount > 0 && neededAgents < agentsCount)
            return agentsForRoads;

        for (int i = 0; i < assignedRoads.Length; i++) {
            agentsForRoads[assignedRoads[i]] = Math.Ceiling(agentsForRoads[assignedRoads[i]] * agentsCount / neededAgents);
            if (agentsForRoads[assignedRoads[i]] < 1)
                agentsForRoads[assignedRoads[i]] = 1;
        }
        return agentsForRoads;
    }

    public SystemAction[] PlanPrepare(AgentStation station, TacticalMap map, DateTime time, bool repeat = false) {
        if (Strength == 0) return Array.Empty<SystemAction>();

        SnowRemover[] agents = station.FreeAgents.OfType<SnowRemover>().ToArray();
        TimeSpan antiIceWorkTime = new TimeSpan(1, 0, 0),
                 waitTime = new TimeSpan(3, 0, 0),
                 holdTime = TimeSpan.Zero,
                 shoveTime = new TimeSpan(3, 0, 0);
        if (Strength > 0)
            holdTime += new TimeSpan(0, 45, 0);
        if (Strength > 1)
            holdTime -= new TimeSpan(0, 30, 0);
        if (Strength > 2)
            shoveTime = new TimeSpan(1, 30, 0);

        if (repeat) {
            waitTime += holdTime;
            holdTime = TimeSpan.Zero;
        }

        var agentsForRoadsAID = DistributeAgentsCount(map.Roads, antiIceWorkTime, map.MapScale, station.AssignedAgents.Length);
        var agentsForRoadsShove = DistributeAgentsCount(map.Roads, shoveTime, map.MapScale, station.AssignedAgents.Length);
        var totalAgentsNeeded = agentsForRoadsAID.Sum(p => p.Value);
        if (agents.Length < totalAgentsNeeded) {
            foreach(var road in agentsForRoadsAID.Keys) {
                agentsForRoadsAID[road] = Math.Round(agentsForRoadsAID[road] * agents.Length / totalAgentsNeeded);
                agentsForRoadsShove[road] = Math.Round(agentsForRoadsShove[road] * agents.Length / totalAgentsNeeded);
            }
        }
        Dictionary<Road, TimeSpan> queueForRoads = new Dictionary<Road, TimeSpan>();
        foreach (var road in agentsForRoadsAID.Keys)
            queueForRoads.Add(road, TimeSpan.Zero);

        List<SystemAction> result = new List<SystemAction>();
        for (int i = 0; i < agents.Length; i++) {
            if (!agentsForRoadsAID.Any(p => p.Value > 0))
                break;
            Road roadToWork = agentsForRoadsAID.First(p => p.Value > 0).Key;

            var takeAIDPlan = Planner.ChangeDevicePlan(agents[i], station, 
                new SnowRemoveDevice(SnowRemoverType.AntiIceDistributor) { MashSpeed = (15 + 10 * TemperatureStrength) / 1000.0 }, time);
            if (!takeAIDPlan.HasValue || takeAIDPlan.Value.action.ExpectedResult.SubjectAfter is not Agent aidAgent) continue;

            var refuelPlan1 = Planner.RefuelPlan(aidAgent, map, takeAIDPlan.Value.action.EndTime, takeAIDPlan.Value.action.EndTime + queueForRoads[roadToWork] + holdTime);
            if (refuelPlan1.HasValue && refuelPlan1.Value.action.ExpectedResult.SubjectAfter is SnowRemover sr && sr.Home is not null && PathFinder.Distance(sr.Home.Position, sr.Position) < sr.Pathfinder?.Scale)
                takeAIDPlan.Value.action.Next.Add(refuelPlan1.Value.action);
            else if (refuelPlan1.HasValue)
                takeAIDPlan.Value.action.Next.Add(refuelPlan1.Value.goAction);

            var deicingPlan = Planner.WorkOnRoad(takeAIDPlan.Value.action.ExpectedResult.SubjectAfter as SnowRemover ?? throw new Exception(),
                                                 roadToWork, takeAIDPlan.Value.action.EndTime, takeAIDPlan.Value.action.EndTime + antiIceWorkTime);
            if (!deicingPlan.HasValue) continue;
            agentsForRoadsAID[roadToWork]--;
            if (refuelPlan1.HasValue)
                refuelPlan1.Value.action.Next.Add(deicingPlan.Value.goAction);
            else
                takeAIDPlan.Value.action.Next.Add(deicingPlan.Value.goAction);

            if (Strength != 1 && !repeat) waitTime = TimeSpan.Zero;
            if (deicingPlan.Value.workAction.Subject is not SnowRemover sr2) throw new ArgumentException(nameof(deicingPlan.Value.workAction.Subject));
            var lastDeicingAction = deicingPlan.Value.returnAction ?? deicingPlan.Value.workAction;

            var takeShovelsPlan = Planner.ChangeDevicePlan(sr2, station, SnowRemoverType.Shovel, lastDeicingAction.EndTime + waitTime + queueForRoads[roadToWork]);
            if (!takeShovelsPlan.HasValue) continue;

            var refuelPlan2 = Planner.RefuelPlan(sr2, map, lastDeicingAction.EndTime, lastDeicingAction.EndTime + waitTime + queueForRoads[roadToWork]);
            if (refuelPlan2.HasValue) {
                lastDeicingAction.Next.Add(refuelPlan2.Value.goAction);
                refuelPlan2.Value.action.Next.Add(takeShovelsPlan.Value.goAction);
            } else {
                lastDeicingAction.Next.Add(takeShovelsPlan.Value.goAction);
            }

            agentsForRoadsShove = DistributeAgentsCount(map.Roads, shoveTime, map.MapScale, station.AssignedAgents.Length);
            var shovePlan = Planner.WorkOnRoad(takeShovelsPlan.Value.action.ExpectedResult.SubjectAfter as SnowRemover ?? throw new Exception(),
                                               roadToWork, takeShovelsPlan.Value.action.EndTime, takeShovelsPlan.Value.action.EndTime + shoveTime);
            if (!shovePlan.HasValue) continue;
            agentsForRoadsShove[roadToWork]--;
            takeShovelsPlan.Value.action.Next.Add(shovePlan.Value.goAction);

            if (takeAIDPlan.HasValue)
                result.Add(takeAIDPlan.Value.goAction);
            else if (refuelPlan1.HasValue)
                result.Add(refuelPlan1.Value.goAction);
            else continue;
            queueForRoads[roadToWork] += new TimeSpan(0, 30, 0);
        }
        return result.ToArray();
    }

    public void Simulate(object? sender, DateTime time) {
        if (sender is not Director director) return;

        GetSnowfallStrength(director.Map.Stations.OfType<Meteostation>().ToArray());
        GetTemperatureStrength(director.Map.Stations.OfType<Meteostation>().ToArray());
    }
}
