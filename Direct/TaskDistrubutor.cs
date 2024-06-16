using System.ComponentModel;

namespace SRDS.Direct {
    using Agents;
    using Agents.Drones;
    using Map.Stations;
    using Map.Targets;
    public partial class Director {

        #region Distribute
        public void DistributeTask() {
            DistributeTaskForFreeAgents();
            DistributeTaskForWorkingTransporters();
        }

        private void DistributeTaskForFreeAgents() {
            var freeAgents = new List<Agent>(FreeAgents).ToArray();
            if (freeAgents.Length > 0 && FreeTargets.Length > 0) {
                CalculateTrajectoryForFreeAgents(freeAgents.Where(p => p.AttachedObj == null).ToArray());
                FindNearestAgentWithTrajectoryForTarget();
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FreeTargets)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CollectedTargets)));
        }
        private void CalculateTrajectoryForFreeAgents(Agent[] freeAgents) {
            //распределение ближайших целей по роботам
            for (int i = 0; i < freeAgents.Length; i++) {
                Agent agent = freeAgents[i];
				Target? targetPos = Qualifier.RecommendTargetForAgent(agent, FreeTargets.Where(p => !agent.BlockedTargets.Contains(p)));
                if (targetPos == null)
                    continue;

                LinkTargetToAgent(agent, targetPos);

                if (!agent.Trajectory.Any()) {
                    agent.AttachedObj = null;
                    agent.BlockedTargets.Add(targetPos);
                }
            }
        }
        private void FindNearestAgentWithTrajectoryForTarget() {
            for (int i = 0; i < FreeTargets.Length; i++) {
                Target t = FreeTargets[i];
                var AttachedAgents = Agents.Where(p => p.AttachedObj == t && (p.CurrentState == RobotState.Ready ||
                        p.CurrentState == RobotState.Thinking)).ToArray();
                if (AttachedAgents?.Length > 0) {
                    t.ReservedAgent = AttachedAgents.MaxBy(p => Qualifier.Qualify(p, t));
                    for (int j = 0; j < AttachedAgents.Length; j++) {
                        if (AttachedAgents[j] != t.ReservedAgent)
                            UnlinkTargetFromAgent(AttachedAgents[j]);
                    }
                }
            }
        }
        private void DistributeTaskForWorkingTransporters() {
            var WorkingTransporters = Agents.Where(
                p => p is Transporter && p.CurrentState == RobotState.Working && Map.Stations
                .Where(p => p is CollectingStation)
                .All(b => PathFinder.Distance(b.Position, p.TargetPosition) > p.InteractDistance))
                .ToList();
            if (WorkingTransporters.Count > 0) {
                for (int i = 0; i < WorkingTransporters.Count; i++) {
                    Transporter transporter = (Transporter)WorkingTransporters[i];
                    CollectingStation? nearBase = (CollectingStation?)Map.Stations.Where(p => p is CollectingStation).MinBy(p => PathFinder.Distance(p.Position, transporter.Position));
                    if (nearBase == null)
                        return;
                    if ((nearBase.Position - transporter.BackTrajectory[^1]).Length < transporter.InteractDistance/2) {
                        transporter.Trajectory = transporter.BackTrajectory.ToList();
                        if (transporter.Trajectory[^1] != nearBase.Position)
                            transporter.Trajectory[^1] = (nearBase.Position);
                        transporter.BackTrajectory = null;
                        transporter.AttachedObj.ReservedAgent = transporter;
                    }
                    else if (PathFinder.Distance(transporter.TargetPosition, nearBase.Position) > transporter.InteractDistance) {
                        transporter.TargetPosition = nearBase.Position;
                        transporter.AttachedObj.ReservedAgent = transporter;
                    } else {
                        transporter.CurrentState = RobotState.Ready;
                    }
                }
            }
        }

        #region Links
        private void LinkTargetToAgent(Agent agent, Target target) {
            if (target == null)
                return;
            agent.AttachedObj = target;
            agent.TargetPosition = target.Position;
        }
        private void UnlinkTargetFromAgent(Agent agent) {
            agent.AttachedObj = null;
            agent.Trajectory.Clear();
            agent.CurrentState = RobotState.Ready;
        }
        #endregion

        #endregion
    }
}
