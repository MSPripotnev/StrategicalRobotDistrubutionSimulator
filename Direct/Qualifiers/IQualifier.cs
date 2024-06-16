namespace SRDS.Direct.Qualifiers {
	using SRDS.Agents;
	using SRDS.Map.Targets;
	public interface IQualifier {
		public double Qualify(Agent agent, Target target);
		public Target? RecommendTargetForAgent(Agent agent, IEnumerable<Target> targets);
	}
}
