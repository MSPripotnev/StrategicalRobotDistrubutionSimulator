using FuzzyLogic.Inference.Engines.Base;

using SRDS.Direct.Strategical;
using SRDS.Model.Targets;

namespace SRDS.Analyzing;
public class Learning {
    StrategyTaskQualifyReading[] best = Array.Empty<StrategyTaskQualifyReading>();
    public Learning() { }
    private double forgetRate = 0.001, learnRate = 0.02;
    const int survivors = 20;
    public List<double> CurrentWeights { get; private set; } = new List<double>();
    public void Mutate(ref object engine) {
        if (engine is not FuzzyInferenceEngine eng) { return; }
        var rs = eng.Rulebase.GetAllRules();
        CurrentWeights.Clear();
        double qEnv = 1, qL = 5, qF = 2, fuelCostRub = 70, deicingCostRub = 80;

        var survived = best.OrderByDescending(
            p => (qEnv * p.RealRemovedSnow + qL * p.RealRemovedIcy + qF * (p.RealFuelCost * fuelCostRub + p.RealDeicingCost * deicingCostRub)) 
                                        / p.TaskTime.TotalSeconds).Take(survivors);
        for (int i = 0; i < rs.Count; i++) {
            var c = rs[i].Conditions.First();
            if (c.Weight > 0)
                c.SetWeight(FuzzyLogic.UnitInterval.Create(Math.Max(0, c.Weight - forgetRate)));
            if (survived.Any(p => p.Rules.OrderByDescending(p => p.Value).ToDictionary(p => p.Key, i => i.Value).ContainsKey(rs[i].Label.Value))) {
                double learnRateR = learnRate * survived.Sum(p => p.Rules.ContainsKey(rs[i].Label.Value) ? p.Rules[rs[i].Label.Value] : 0);
                c.SetWeight(FuzzyLogic.UnitInterval.Create(Math.Min(1, c.Weight + learnRateR)));
            }
            CurrentWeights.Add(Math.Round(c.Weight.Value, 4));
        }
    }
    /// <summary>
    /// Select best epoch as max qualified array of readings
    /// </summary>
    /// <param name="qualifyReadings">Array of epochs where epoch is array of readings</param>
    /// <param name="systemQuality">List of epoch system qualifies</param>
    public void Select(StrategyTaskQualifyReading[][] qualifyReadings, List<double> systemQuality) {
        best = qualifyReadings[systemQuality.IndexOf(systemQuality.Max())];
    }
}
