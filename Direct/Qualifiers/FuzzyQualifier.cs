namespace SRDS.Direct.Qualifiers {
	using FuzzyLogic.Fuzzification;
	using FuzzyLogic.Inference;
	using FuzzyLogic.Logic;
	using FuzzyLogic.Logic.Operators;
	using FuzzyLogic.MembershipFunctions;

	using SRDS.Agents;
	using SRDS.Agents.Drones;
	using SRDS.Map.Targets;

	public class FuzzyQualifier : IQualifier {
		private readonly FuzzyLogic.BinaryOperations.ITriangularNorm TNorm 
			= new FuzzyLogic.BinaryOperations.EinsteinProduct();
		private readonly FuzzyLogic.BinaryOperations.ITriangularConorm TConorm 
			= new FuzzyLogic.BinaryOperations.BoundedSum();
		public enum Values {
			Low, High
		}
		static LinguisticVariable Quality {
			get {
				double start = 0.1, end = 0.8;
				FuzzySet[] fs = new FuzzySet[] {
					new FuzzySet(Values.Low, TrapezoidalFunction.CreateWithLeftEdge(start, end)),
					new FuzzySet(Values.High, TrapezoidalFunction.CreateWithRightEdge(start, end))
				};
				return new LinguisticVariable("Quality", fs);
			}
		}
		public object Net;
		public FuzzyLogic.Inference.Engines.Base.FuzzyInferenceEngine Engine;
		public FuzzyQualifier() {
			SnowRemover r;
			Snowdrift s;
			Dictionary<string, (double min, double max)> values = new Dictionary<string, (double min, double max)>() {
				{ "DistanceToTarget", (50, 400) },
				{ nameof(r.Fuel), (0, 100) },
				{ nameof(s.Level), (0, 40) },
				{ nameof(s.MashPercent), (0, 100)},
				{ nameof(r.RemoveSpeed), (0.5, 1.0)},
				{ nameof(r.MashSpeed), (1.0, 2.0)}
			};

			Engine = new FuzzyLogic.Inference.Engines.SugenoInferenceEngine(TNorm, TConorm,
				new FuzzyLogic.Defuzzification.CentroidDefuzzifier());
			Net = Engine;
			List<LinguisticVariable> lvs = new List<LinguisticVariable>();
			foreach (var value in values) {
				double sz = value.Value.max - value.Value.min,
						le = value.Value.min + sz / 8.0,
						re = value.Value.min + 7.0/8.0*sz;
				lvs.Add(new LinguisticVariable(value.Key, new FuzzySet[] {
					new FuzzySet(Values.Low, TrapezoidalFunction.CreateWithLeftEdge(le, re)),
					new FuzzySet(Values.High, TrapezoidalFunction.CreateWithRightEdge(le, re)),
				}));
				Engine.Database.AddVariable(FuzzyLogic.Label.Create(value.Key));
			}
			var premises = FormPremises(lvs.Count);
			for (int i = 0; i < 1 << lvs.Count; i++) {
				List<Premise> pr = new List<Premise>();
				for (int j = 0; j < lvs.Count; j++)
					pr.Add(new Premise(j == 0 ? new If() : new And(), lvs[j], new Is(), lvs[j].GetMembers().ToArray()[premises[i,j] ? 1 : 0]));
				Condition[] c = { new Condition(new If(), pr, FuzzyLogic.UnitInterval.Create(0.5)) };
				Conclusion[] cs = {
					new Conclusion(Quality, new Is(), Quality.GetMembers().ToArray()[1]),
					new Conclusion(Quality, new Not(), Quality.GetMembers().ToArray()[0])
				};
				Engine.Rulebase.AddRule(new FuzzyRule(nameof(Quality)+"Rule"+i, c, cs));
			}
		}
		public Target? RecommendTargetForAgent(Agent agent, IEnumerable<Target> targets) {
			Dictionary<Target, double> targetsDict = targets.ToDictionary(p => p, i => Qualify(agent, i));
			var rs = targetsDict.OrderByDescending(x => x.Value).ToArray();
			return targetsDict.Any() ? rs[0].Key : null;
		}
		public double Qualify(Agent agent, Target target) {
			return Qualify(agent, target, out var _);
		}
		public double Qualify(Agent agent, Target target, out Dictionary<string, double> activatedRules) {
			activatedRules = null;
			if (target is not Snowdrift s) return 0;
			if (agent is not SnowRemover r) return 0;
			return Qualify(new Dictionary<string, double>() {
				{ "DistanceToTarget", (agent.Position-target.Position).Length },
				{ nameof(agent.Fuel), agent.Fuel },
				{ nameof(s.Level), s.Level },
				{ nameof(s.MashPercent), s.MashPercent},
				{ nameof(r.RemoveSpeed), r.RemoveSpeed},
				{ nameof(r.MashSpeed), r.MashSpeed},
			}, out activatedRules);
		}
		private double Qualify(Dictionary<string, double> input, out Dictionary<string, double> activatedRules) {
			if (input.Count < Engine.Database.VariableCount)
				throw new ArgumentException("");
			foreach (var v in input)
				Engine.Database.UpdateData(new FuzzyLogic.DataPoint(FuzzyLogic.Label.Create(v.Key), v.Value));
			var ls = new Dictionary<FuzzyRule, IList<FuzzyOutput>>();
			foreach (var r in Engine.Rulebase.GetAllRules())
				ls.Add(r, r.Evaluate(Engine.Database.GetAllDataLabeled(), new FuzzyEvaluator(TNorm, TConorm)));
			activatedRules = ls.Where(p => p.Value.First().FiringStrength.Value > 0)
				.ToDictionary(p => p.Key.Label.Value, i => i.Value.First().FiringStrength.Value);
			return activatedRules.Sum(p => p.Value);
		}

		private static bool[,] FormPremises(int n) {
			bool[,] arr = new bool[1 << n, n];
			for (int i = 0; i < n; i++) {
				bool curr = false;
				int freq = 1 << i;
				for (int j = 0; j < 1 << n;) {
					for (int k = 0; k < freq; k++, j++)
						arr[j, i] = curr;
					curr = !curr;
				}
			}
			return arr;
		}
	}
}
