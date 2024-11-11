using System.Xml.Serialization;

using SRDS.Model.Targets;

namespace SRDS.Direct.Tactical;
public class DistributionQualifyReading {
    public string ModelName { get; set; }
    public Target TakedTarget { get; set; }
    public System.Windows.Point AgentPosition { get; set; }
    public double WorkingTime { get; set; } = 0;
    public double WayTime { get; set; } = 0;
    public double TakedLevel { get; set; } = 0;
    [XmlIgnore]
    public double SumTime { get => WorkingTime + WayTime; }
    public double FuelCost { get; set; }
    [XmlIgnore]
    public Dictionary<string, double> Rules {
        get {
            var res = new Dictionary<string, double>();
            for (int i = 0; i < RulesActivated.Length; i++)
                res.Add(RulesActivated[i], FiringStrength[i]);
            return res;
        }
        set {
            if (value != null && value.Keys.Any()) {
                RulesActivated = value.Keys.ToArray();
                FiringStrength = value.Values.ToArray();
            }
        }
    }
    public string[] RulesActivated { get; set; }
    public double[] FiringStrength { get; set; }
    public DistributionQualifyReading() {

    }
}
