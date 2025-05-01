using System.Xml.Serialization;

using SRDS.Direct.Executive;

namespace SRDS.Direct.Strategical; 
public class StrategyTaskQualifyReading {
    public string ModelName { get; set; }
    public TimeSpan TaskTime { get; set; } = TimeSpan.Zero;
    public SnowRemoverType Device { get; set; } = SnowRemoverType.PlowBrush;
    public double RemovedSnow { get; set; } = 0;
    public double RemovedIcy { get; set; } = 0;
    public double FuelCost { get; set; } = 0;
    public double DeicingCost { get; set; } = 0;
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
    public StrategyTaskQualifyReading() {
    }
}
