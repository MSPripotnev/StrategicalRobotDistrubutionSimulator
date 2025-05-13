using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SRDS.Direct.Strategical;
using SRDS.Model;
using SRDS.Model.Map.Stations;
using SRDS.Model.Map;
using SRDS.Direct.Tactical.Qualifiers;
using SRDS.Direct.Agents.Drones;
using SRDS.Direct.Executive;
using SRDS.Analyzing;
using System.IO;
using System.Xml.Serialization;

namespace SRDS.Direct.ControlSystem;
public class AISnowRemoveControlSystem : IPlanningControlSystem {
    public FuzzyQualifier DeviceQualifier, WorkQualifier;
    Learning Learning;
    int seed = 0;
    public static Dictionary<string, (double min, double max)> workQualifyInputs = new Dictionary<string, (double min, double max)>() {
            { nameof(SnowRemover.Fuel), (10, 350) },
            { nameof(SnowRemoveDevice.MashSpeed), (0, 0.015)},
            { nameof(SnowRemoveDevice.RemoveSpeed), (10.0, 20.0)},
            { "DistanceToRoad", (40, 600) },
            { nameof(Road.Category), (0, 5) },
            { nameof(Road.Length), (150, 1000) },
            { nameof(Meteostation.Humidity), (0, 100) },
            { nameof(Meteostation.Pressure), (740, 770) },
            { nameof(Meteostation.Temperature), (-20, 10) },
            { nameof(Meteostation.HumidityChange), (-5, 5) },
            { nameof(Meteostation.PressureChange), (-5, 5) },
            { nameof(Meteostation.TemperatureChange), (-5, 5) },
            { nameof(Meteostation.WindSpeed), (0, 6) },
            { nameof(Meteostation.PrecipitationIntensity), (0, 1) },
            { nameof(Meteostation.CloudnessType), (0, 2)},
        };
    public static Dictionary<string, (double min, double max)> deviceTypeQualifyInputs = new Dictionary<string, (double min, double max)>() {
            { nameof(SnowRemover.Fuel), (0, 350) },
            { nameof(SnowRemoveDevice.FuelRate), (0, 0.05) },
            { nameof(SnowRemoveDevice.MashSpeed), (0, 0.015)},
            { nameof(SnowRemoveDevice.RemoveSpeed), (10.0, 20.0)},
            { nameof(Meteostation.Humidity), (0, 100) },
            { nameof(Meteostation.Pressure), (740, 770) },
            { nameof(Meteostation.Temperature), (-25, 10) },
            { nameof(Meteostation.HumidityChange), (-5, 5) },
            { nameof(Meteostation.PressureChange), (-5, 5) },
            { nameof(Meteostation.TemperatureChange), (-5, 5) },
            { nameof(Meteostation.WindSpeed), (0, 6) },
            { nameof(Meteostation.PrecipitationIntensity), (0, 1) },
            { nameof(Meteostation.CloudnessType), (0, 2)},
        };

    [XmlIgnore]
    public Dictionary<SystemAction, StrategyTaskQualifyReading> StrategyQualifyRecordings { get; set; } = new();
    public AISnowRemoveControlSystem(int _seed = 0) {
        Learning = new Learning();
        seed = _seed;
        WorkQualifier = new FuzzyQualifier(workQualifyInputs);
        DeviceQualifier = new FuzzyQualifier(deviceTypeQualifyInputs);
    }
    public SystemAction[] PlanPrepare(AgentStation station, TacticalMap map, DateTime time, bool repeat = false) {
        StrategicQualifier.Qualify(station, map.Stations.OfType<Meteostation>().First(),
            station.AssignedRoads, DeviceQualifier, WorkQualifier,
            out var agentToDeviceQualifies, out var agentToRoadQualifies,
            out var agentToDeviceRules, out var agentToRoadRules);

        List<SystemAction> actions = new List<SystemAction>();
        SystemAction? changeDeviceAction = null, workAction = null;
        var agents = station.FreeAgents.OfType<SnowRemover>().ToArray();
        for (int i = 0; i < agents.Length; i++) {
            var desiredDevice = agentToDeviceQualifies[agents[i]].MaxBy(p => p.Value);
            var equalQualifiedDevices = agentToDeviceQualifies[agents[i]].Where(p => p.Value == desiredDevice.Value).ToArray();
            if (equalQualifiedDevices.Length > 1)
                desiredDevice = equalQualifiedDevices[(new Random(seed)).Next(0, equalQualifiedDevices.Length)];

            if (agents[i].Devices.FirstOrDefault(p => p?.Type == desiredDevice.Key.Type, null) is null)
                changeDeviceAction = Planner.ChangeDevicePlan(agents[i], desiredDevice.Key, time);

            if (agentToRoadQualifies[agents[i]].Any()) {
                var selectedRoad = agentToRoadQualifies[agents[i]].MaxBy(p => p.Value).Key;
                for (int j = i; j < agents.Length; j++)
                    agentToRoadQualifies[agents[j]].Remove(selectedRoad);
                workAction = Planner.WorkOnRoad(agents[i], selectedRoad, time, time.AddMinutes(30));
            }

            if (changeDeviceAction is not null) {
                if (changeDeviceAction.Object is SnowRemoverType type && type == SnowRemoverType.AntiIceDistributor)
                    Planner.RefuelPlan(agents[i], map, time);
                if (workAction is not null)
                    changeDeviceAction.Next.Add(workAction);
                actions.Add(changeDeviceAction);
            } else if (workAction is not null) {
                actions.Add(workAction);
            }
        }
        return actions.ToArray();
    }
    public void Simulate(object? sender, DateTime time) {
        if (sender is not Director director || time.Minute % 10 != 0) return;

        Learning.Select(director.Recorder.AllEpochsTaskQualifyReadings, director.Recorder.SystemQuality);
        Learning.Mutate(ref DeviceQualifier.Net);
        Learning.Mutate(ref WorkQualifier.Net);
    }
}
