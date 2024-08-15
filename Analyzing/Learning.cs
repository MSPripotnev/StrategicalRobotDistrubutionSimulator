using FuzzyLogic.Inference.Engines.Base;
using SRDS.Direct.Strategical;
using SRDS.Model.Targets;

namespace SRDS.Analyzing;
public class Learning {
    DistributionQualifyReading[] best = Array.Empty<DistributionQualifyReading>();
    public Learning() { }
    private double forgetRate = 0.001, learnRate = 0.02;
    const int survivors = 20;
    public List<double> CurrentWeights { get; private set; } = new List<double>();
    public void Mutate(ref object eng) {
        var rs = (eng as FuzzyInferenceEngine).Rulebase.GetAllRules();
        CurrentWeights.Clear();
        var survived = best.OrderByDescending(p => (p.TakedLevel - (p.TakedTarget as Snowdrift).Level) / p.SumTime).Take(survivors);
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
    public void Select(Recorder recorder) {
        best = recorder.QualifyReadings[recorder.SystemQuality.IndexOf(recorder.SystemQuality.Max())];
    }
}
