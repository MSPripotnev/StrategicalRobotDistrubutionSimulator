namespace SRDS.Direct.Strategical.Qualifiers;
using Agents;
using Model.Targets;

public interface IQualifier {
    public double Qualify(Agent agent, Target target);
    public Target? RecommendTargetForAgent(Agent agent, IEnumerable<Target> targets);
}
