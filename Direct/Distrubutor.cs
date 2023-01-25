using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace TacticalAgro {
    public partial class Director {

        #region Distribute
        public void DistributeTask() {
            lock (Transporters) {
                var FreeTransporters = Transporters.Where(p => p.CurrentState == RobotState.Ready);
                DistributeTaskForFreeTransporters();
                DistributeTaskForCarryingTransporters();
            }
        }

        //readonly Dictionary<Transporter, Task<Point[]>> trajectoryTasks = new Dictionary<Transporter, Task<Point[]>>();
        private void DistributeTaskForFreeTransporters() {
            var freeTransport = new List<Transporter>(FreeTransporters).ToArray();
            lock (freeTransport)
                if (freeTransport.Length > 0 && FreeTargets.Length > 0) {
                    CalculateTrajectoryForFreeTransporters(freeTransport);
                    lock (Targets)
                        SelectNearestTransporterWithTrajectoryForTarget();
                }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FreeTargets)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CollectedTargets)));
        }
        private void CalculateTrajectoryForFreeTransporters(Transporter[] freeTransport) {
            //распределение ближайших целей по роботам
            for (int i = 0; i < freeTransport.Length; i++) {
                Transporter transporter = freeTransport[i];
                Target? nearestTarget = FreeTargets.Where(p => !transporter.BlockedTargets.Contains(p)).MinBy(
                    t => PathFinder.Distance(t.Position, transporter.Position));
                if (nearestTarget == null)
                    continue;

                DateTime startTime = DateTime.Now;
                LinkTargetToTransporter(transporter, nearestTarget);
                ThinkingTime += (DateTime.Now - startTime);

                if (!transporter.Trajectory.Any()) {
                    transporter.AttachedObj = null;
                    transporter.BlockedTargets.Add(nearestTarget);
                }
            }
        }
        private void SelectNearestTransporterWithTrajectoryForTarget() {
            for (int i = 0; i < FreeTargets.Length && FreeTransporters.Any(); i++) {
                Target t = FreeTargets[i];
                var AttachedTransporters = FreeTransporters.Where(p => p.AttachedObj == t).ToArray();
                if (AttachedTransporters != null && AttachedTransporters.Length > 0) {
                    t.ReservedTransporter = AttachedTransporters.MinBy(p => p.DistanceToTarget);
                    for (int j = 0; j < AttachedTransporters.Length; j++) {
                        if (AttachedTransporters[j] != t.ReservedTransporter) {
                            UnlinkTargetFromTransporter(AttachedTransporters[j]);
                        } else {
                            AttachedTransporters[j].CurrentState = RobotState.Going;
                        }
                    }
                }
            }
        }
        private void DistributeTaskForCarryingTransporters() {
            var CarryingTransporters = Transporters.Where(
                p => p.CurrentState == RobotState.Carrying &&
                Map.Bases.All(b => PathFinder.Distance(b.Position, p.TargetPosition) > p.InteractDistance)
                ).ToList();
            if (CarryingTransporters.Count > 0) {
                Task<Point[]>[] trajectoryTasks = new Task<Point[]>[CarryingTransporters.Count];
                DateTime startTime = DateTime.Now;
                for (int i = 0; i < CarryingTransporters.Count; i++) {
                    Transporter transporter = CarryingTransporters[i];
                    var nearBase = Map.Bases.MinBy(p => PathFinder.Distance(p.Position, transporter.Position));
                    if (PathFinder.Distance(transporter.TargetPosition, nearBase.Position) > transporter.InteractDistance) {
                        transporter.TargetPosition = nearBase.Position;
                        transporter.AttachedObj.ReservedTransporter = transporter;
                    } else {
                        transporter.CurrentState = RobotState.Ready;
                    }
                }
                ThinkingTime += (DateTime.Now - startTime);
            }
        }

        #region Main
        private void LinkTargetToTransporter(Transporter transporter, Target target) {
            if (target == null)
                return;
            transporter.AttachedObj = target;
            transporter.TargetPosition = target.Position;
        }
        private void UnlinkTargetFromTransporter(Transporter transporter) {
            transporter.AttachedObj = null;
            transporter.Trajectory.Clear();
            transporter.CurrentState = RobotState.Ready;
        }
        #endregion

        #endregion
    }
}
