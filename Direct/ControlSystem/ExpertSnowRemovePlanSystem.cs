namespace SRDS.Direct.ControlSystem;
using Agents;
using Model;
using Model.Map.Stations;

using SRDS.Direct.Agents.Drones;
using SRDS.Model.Map;

using Strategical;
public class ExpertSnowRemovePlanSystem : ITimeSimulatable {
    private int strength;
    public int Strength {
        get => strength;
        set {
            if (strength == value) return;
            strength = value;
        }
    }
    private int temperatureStrength;

    private void GetSnowfallStrength(Meteostation[] meteostations) {
        if (!meteostations.Any()) return;
        var fallSpeed = meteostations.Sum(p => p.PrecipitationIntensity) / meteostations.Length;
        Strength = fallSpeed > 3 ? 3 : (fallSpeed >= 1 ? 2 : (fallSpeed >= 0.5 ? 1 : 0));
    }

    private void GetTemperatureStrength(Meteostation[] meteostations) {
        if (!meteostations.Any()) return;
        var temperature = meteostations.Sum(p => p.Temperature) / meteostations.Length;
        temperatureStrength = temperature > -6 ? 2 : (temperature >= -18 ? 1 : 0);
    }

    private SystemAction[] PlanPrepare(AgentStation station, TacticalMap map, DateTime time) {
        var agents = station.AssignedAgents.OfType<SnowRemover>().ToArray();
        TimeSpan holdTime = TimeSpan.Zero;
        if (Strength > 0)
            holdTime += new TimeSpan(0, 45, 0);
        if (Strength > 1)
            holdTime -= new TimeSpan(0, 30, 0);

        List<SystemAction> result = new List<SystemAction>();
        for (int i = 0; i < agents.Length; i++) {
            var changeDevicePlan = Planner.ChangeDevicePlan(agents[i], station, SnowRemoverType.AntiIceDistributor, time);
            if (!changeDevicePlan.HasValue) continue;

            TimeSpan workTime = new TimeSpan(1, 0, 0);
            var workOnPlan = Planner.WorkOnRoad(agents, /*make them select roads themself*/, changeDevicePlan.Value.action.EndTime, changeDevicePlan.Value.action.EndTime + workTime);
            if (!workOnPlan.HasValue) continue;
            changeDevicePlan.Value.action.Next = workOnPlan.Value.goAction;

            TimeSpan waitTime = new TimeSpan(3, 0, 0);
            if (Strength != 1) waitTime = TimeSpan.Zero;
            var changeDevicePlan2 = Planner.ChangeDevicePlan(agents[i], station, /*make them select devices*/, (workOnPlan.Value.returnAction ?? workOnPlan.Value.workAction).EndTime + waitTime);
            if (!changeDevicePlan2.HasValue) continue;
            (workOnPlan.Value.returnAction ?? workOnPlan.Value.workAction).Next = changeDevicePlan2.Value.goAction;

            result.Add(changeDevicePlan.Value.goAction);
        }
        return result.ToArray();
    }

    public void Simulate(object? sender, DateTime time) {
        if (sender is not Director director) return;
        if (time.Second != 0 || time.Minute != 0 || time.Hour % 2 != 0) return;

        GetSnowfallStrength(director.Map.Stations.OfType<Meteostation>().ToArray());
        GetTemperatureStrength(director.Map.Stations.OfType<Meteostation>().ToArray());

        var agentStations = director.Map.Stations.OfType<AgentStation>().ToArray();
        for (int i = 0; i < agentStations.Length; i++) {
            PlanPrepare(agentStations[i], director.Map, time);
        }
    }
}
