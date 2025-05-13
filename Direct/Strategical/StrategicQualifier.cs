namespace SRDS.Direct.Strategical;

using System.IO;
using System.Windows;

using Agents;
using Agents.Drones;

using Executive;

using Model.Map;
using Model.Map.Stations;

using SRDS.Direct.Tactical.Qualifiers;

using Tactical;

public enum ActionRecommendation {
    Approve,
    Delay,
    IncreasePower
}

public class StrategicQualifier {
    public StrategicQualifier() { 
    }
    
    public static ActionResult Recognize(Director director, SystemAction action, DateTime time) {
        IControllable? subject;
        {
            if (action.Subject is AgentStation station)
                subject = director.Map.Stations.OfType<AgentStation>().First(p => p == station);
            else if (action.Subject is Agent agent)
                subject = director.Agents.First(p => p.Equals(agent));
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
    public static void Qualify(AgentStation station, Meteostation meteostation, Road[] roads, FuzzyQualifier deviceQualifier, FuzzyQualifier workQualifier,
            out Dictionary<SnowRemover, Dictionary<SnowRemoveDevice, double>> agentToDeviceQualifies, 
            out Dictionary<SnowRemover, Dictionary<Road, double>> agentToRoadQualifies,
            out Dictionary<SnowRemoveDevice, Dictionary<string, double>> activatedRulesForDevice,
            out Dictionary<Road, Dictionary<string, double>> activatedRulesForRoad) {
        agentToRoadQualifies = new Dictionary<SnowRemover, Dictionary<Road, double>>();
        agentToDeviceQualifies = new Dictionary<SnowRemover, Dictionary<SnowRemoveDevice, double>>();
        activatedRulesForDevice = new();
        activatedRulesForRoad = new();
        if (!station.AssignedAgents.Any()) return;

        SnowRemover[] snowRemovers = station.FreeAgents.OfType<SnowRemover>().ToArray();
        Dictionary<Road, double> roadsMaxQualifies = new();
        for (int i = 0; i < snowRemovers.Length; i++) {
            SnowRemover snowRemover = snowRemovers[i];
            Qualify(snowRemover, meteostation, roads, deviceQualifier, workQualifier, ref roadsMaxQualifies,
                out var roadsQualifyForAgent, out var deviceQualifyForAgent);
            agentToDeviceQualifies.Add(snowRemover, deviceQualifyForAgent);
            agentToRoadQualifies.Add(snowRemover, roadsQualifyForAgent);
        }

        if (station.FreeAgents.Length < roadsMaxQualifies.Count) {
            var t = roadsMaxQualifies.OrderBy(p => p.Value).Take(roadsMaxQualifies.Count - station.FreeAgents.Length).ToArray();
            for (int i = 0; i < t.Length; i++) {
                roadsMaxQualifies.Remove(t[i].Key);
                for (int j = 0; j < snowRemovers.Length; j++)
                    agentToRoadQualifies[snowRemovers[j]].Remove(t[i].Key);
            }
        }
    }
    public static void Qualify(SnowRemover snowRemover, Meteostation meteostation, Road[] roads, 
            FuzzyQualifier deviceQualifier, FuzzyQualifier workQualifier,
            ref Dictionary<Road, double> roadsMaxQualifies,
            out Dictionary<Road, double> roadsQualifyForAgent,
            out Dictionary<SnowRemoveDevice, double> deviceQualifyForAgent) {
        roadsQualifyForAgent = new Dictionary<Road, double>();
        deviceQualifyForAgent = new Dictionary<SnowRemoveDevice, double>();
        roadsMaxQualifies = new Dictionary<Road, double>();
        var meteoInputs = new Dictionary<string, double>() {
                { nameof(meteostation.Humidity), meteostation.Humidity },
                { nameof(meteostation.Pressure), meteostation.Pressure },
                { nameof(meteostation.Temperature), meteostation.Temperature },
                { nameof(meteostation.HumidityChange), meteostation.HumidityChange },
                { nameof(meteostation.PressureChange), meteostation.PressureChange },
                { nameof(meteostation.TemperatureChange), meteostation.TemperatureChange },
                { nameof(meteostation.WindSpeed), meteostation.WindSpeed },
                { nameof(meteostation.PrecipitationIntensity), meteostation.PrecipitationIntensity },
                { nameof(meteostation.CloudnessType), (int)meteostation.CloudnessType},
            };
        var devices = typeof(SnowRemoverType).GetEnumValues();
        for (int j = 0; j < devices.Length; j++) {
            if (devices.GetValue(j) is not SnowRemoveDevice device) continue;
            deviceQualifyForAgent.Add(device, Qualify(snowRemover, device, meteoInputs, deviceQualifier));
        }
        for (int j = 0; j < roads.Length; j++) {
            Road road = roads[j];
            double q = Qualify(snowRemover, road, meteoInputs, workQualifier);
            roadsQualifyForAgent.Add(road, q);
            if (!roadsMaxQualifies.ContainsKey(road))
                roadsMaxQualifies.Add(road, q);
            else
                roadsMaxQualifies[road] = Math.Max(roadsMaxQualifies[road], q);
        }
    }
    public static double Qualify(SnowRemover snowRemover, SnowRemoveDevice device, Dictionary<string, double> meteoInputs, FuzzyQualifier deviceQualifier) {
        var deviceQualifyInput = new Dictionary<string, double>() {
                    { nameof(snowRemover.Fuel), snowRemover.Fuel },
                    { nameof(device.MashSpeed), snowRemover.MashSpeed},
                    { nameof(device.RemoveSpeed), snowRemover.RemoveSpeed},
                    { nameof(device.FuelRate), device.FuelRate }
                };
        deviceQualifyInput = deviceQualifyInput.Concat(meteoInputs).ToDictionary(p => p.Key, i => i.Value);
        return deviceQualifier.Qualify(deviceQualifyInput, out var activatedRules);
    }
    public static double Qualify(SnowRemover snowRemover, Road road, Dictionary<string, double> meteoInputs, FuzzyQualifier workQualifier) {
        double DistanceToRoad = road.DistanceToRoad(snowRemover.Position);
        var workQualifyInput = new Dictionary<string, double>() {
                    { nameof(snowRemover.Fuel), snowRemover.Fuel },
                    { nameof(snowRemover.MashSpeed), snowRemover.MashSpeed},
                    { nameof(snowRemover.RemoveSpeed), snowRemover.RemoveSpeed},
                    { nameof(DistanceToRoad), DistanceToRoad },
                    { nameof(road.Category), road.Category },
                    { nameof(road.Length), road.Length },
                };
        workQualifyInput = workQualifyInput.Concat(meteoInputs).ToDictionary(p => p.Key, i => i.Value);
        return workQualifier.Qualify(workQualifyInput, out var activatedRules);
    }
}
