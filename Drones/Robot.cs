using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TacticalAgro {
    public enum RobotState {
        Disable,
        Ready,
        Going,
        Carrying
    }
    public class Robot : IMoveable {
        public PointF Position { get; set; }
        public Target TargetPosition { get; set; }
        public Target AttachedObj { get; set; } = null;
        private const float speed = 1F;
        public Color Color { get; set; } = Color.Red;
        public Robot(Point pos) {
            Position = pos;
            Color = Color.Red;
        }
        public Robot(int X, int Y) {
            Position = new Point(X, Y);
            Color = Color.Red;
        }
        private RobotState state;
        public RobotState State {
            get {
                return state;
            }
            set {
                switch (value) {
                    case RobotState.Disable:
                        break;
                    case RobotState.Ready:
                        break;
                    case RobotState.Going:
                        break;
                    case RobotState.Carrying:
                        break;
                }
            }
        }

        public void Simulate() {
            if (TargetPosition != null) {
                IMoveable p1 = this;
                IMoveable p2 = TargetPosition;
                //вектор движения
                PointF V = new PointF(p2.Position.X - p1.Position.X,
                                    p2.Position.Y - p1.Position.Y);
                //длина вектора
                float d = (float)Director.Distance(p1.Position, p2.Position);
                //нормировка
                if (d > 0) {
                    V.X /= d;
                    V.Y /= d;
                }
                //новое значение
                Position = new PointF(Position.X + V.X * speed, Position.Y + V.Y * speed);
            }
            if (AttachedObj != null) {
                AttachedObj.Position = new PointF(Position.X, Position.Y);
            }
        }
        public Target FindNearestTarget(Target[] targets, double threshold = 100500) {
            double D = threshold + 1;
            Target res = null;
            for (int i = 0; i < targets.Length; i++) {
                if (targets[i].ReservedRobot != null && targets[i].ReservedRobot != this)
                    continue;
                if (targets[i].Finished)
                    continue;

                double dNew = Director.Distance(targets[i].Position, Position);
                if (dNew < D) {
                    D = dNew;
                    res = targets[i];
                }
            }
            if (D > threshold) {
                res = null;
            }
            return res;
        }

        public void Take(Target target) {
            if (target != null)
                AttachedObj = target;
        }
    }
}
