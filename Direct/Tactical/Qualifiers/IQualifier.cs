namespace SRDS.Direct.Tactical.Qualifiers;
using Agents;
using Model.Targets;

public interface IQualifier {
    public double Qualify(IControllable agent, ITargetable target);
    public ITargetable? RecommendTargetForAgent(IControllable agent, IEnumerable<ITargetable> targets);
}
