namespace SRDS.Direct.Tactical.Qualifiers;
using Agents;
using Direct.Executive;
using Model.Targets;

public class DistanceQualifier : IQualifier {
    public double Qualify(Agent agent, Target t) =>
        1.0 / (PathFinder.Distance(t.Position, agent.Position) + 1.0);
    public Target? RecommendTargetForAgent(Agent agent, IEnumerable<Target> targets) {
        Dictionary<Target, double> targetsDict = targets.ToDictionary(p => p, i => Qualify(agent, i));
        return targetsDict.Any() ? targetsDict.MaxBy(p => p.Value).Key : null;
    }
}
