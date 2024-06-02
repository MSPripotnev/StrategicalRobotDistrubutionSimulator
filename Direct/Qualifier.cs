using SRDS.Agents;
using SRDS.Map.Targets;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRDS.Direct {
	public static class Qualifier {
		public static double Qualify(Agent agent, Target t) {
			return 1.0/PathFinder.Distance(t.Position, agent.Position);
		}
		public static Target? RecommendTargetForAgent(Agent agent, IEnumerable<Target> targets) {
			Dictionary<Target, double> targetsDict = targets.ToDictionary(p => p, i => Qualify(agent, i));
			return targetsDict.Any() ? targetsDict.Min().Key : null;
		}
	}
}
