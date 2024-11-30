namespace SRDS.Direct.Tactical.Qualifiers;
using Agents;
using Direct.Executive;
using Model.Targets;

using SRDS.Model.Map;

public class DistanceQualifier : IQualifier {
    public double Qualify(IControllable agent, ITargetable t) =>
        1.0 / (PathFinder.Distance(t is Road r ? (agent.Position ^ r) : t.Position, agent.Position) + 1.0);
    public ITargetable? RecommendTargetForAgent(IControllable agent, IEnumerable<ITargetable> targets) {
        Dictionary<ITargetable, double> targetsDict = targets.ToDictionary(p => p, i => Qualify(agent, i));
        return targetsDict.Any() ? targetsDict.MaxBy(p => p.Value).Key : null;
    }
}
