using System.ComponentModel;

namespace SRDS {
    using Agents;
    using Agents.Drones;
    using Map;
    using Map.Stations;
    using Map.Targets;
    public partial class Director {

        #region Distribute
        public void DistributeTask() {
            DistributeTaskForFreeAgents();
            DistributeTaskForWorkingTransporters();
        }

        private void DistributeTaskForFreeAgents() {
            var freeTransport = new List<Agent>(FreeAgents).ToArray();
            if (freeTransport.Length > 0 && FreeTargets.Length > 0) {
                CalculateTrajectoryForFreeAgents(freeTransport);
                FindNearestAgentWithTrajectoryForTarget();
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FreeTargets)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CollectedTargets)));
        }
        private void CalculateTrajectoryForFreeAgents(Agent[] freeTransport) {
            //распределение ближайших целей по роботам
            for (int i = 0; i < freeTransport.Length; i++) {
                Agent transporter = freeTransport[i];
                IPlaceable targetPos;
                targetPos = FreeTargets.Where(p => !transporter.BlockedTargets.Contains(p) && !Agents.Any(t => t.AttachedObj == p))
                    .MinBy(t => PathFinder.Distance(t.Position, transporter.Position));
                if (targetPos == null)
                    continue;

                LinkTargetToAgent(transporter, targetPos as Target);

                if (!transporter.Trajectory.Any()) {
                    transporter.AttachedObj = null;
                    transporter.BlockedTargets.Add(targetPos as Target);
                }
            }
        }
        private void FindNearestAgentWithTrajectoryForTarget() {
            for (int i = 0; i < FreeTargets.Length && FreeAgents.Any(); i++) {
                Target t = FreeTargets[i];
                var AttachedTransporters = FreeAgents.Where(p => p is Transporter && p.AttachedObj == t).ToArray();
                if (AttachedTransporters != null && AttachedTransporters.Length > 0) {
                    t.ReservedAgent = AttachedTransporters.MinBy(p => p.DistanceToTarget);
                    for (int j = 0; j < AttachedTransporters.Length; j++) {
                        if (AttachedTransporters[j] != t.ReservedAgent) {
                            UnlinkTargetFromTransporter((Transporter)AttachedTransporters[j]);
                        } else {
                            AttachedTransporters[j].CurrentState = RobotState.Going;
                        }
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

        #region Main
        private void LinkTargetToAgent(Agent transporter, Target target) {
            if (target == null)
                return;
            transporter.AttachedObj = target;
            transporter.TargetPosition = target.Position;
        }
        private void UnlinkTargetFromTransporter(Agent transporter) {
            transporter.AttachedObj = null;
            transporter.Trajectory.Clear();
            transporter.CurrentState = RobotState.Ready;
        }
        #endregion

        #endregion
    }
}
