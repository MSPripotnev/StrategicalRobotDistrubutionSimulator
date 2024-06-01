﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;
using System.Xml.Serialization;
using SRDS.Map;
using SRDS.Map.Stations;
using SRDS.Map.Targets;
using SRDS.Agents.Drones;

namespace SRDS.Agents {
    public enum RobotState {
		Disable = -1,
		Ready,
		Thinking,
		Going,
		Working,
		Broken
	}
	[XmlInclude(typeof(SnowRemover))]
	[XmlInclude(typeof(Transporter))]
	public abstract class Agent : IPlaceable, IDrone, INotifyPropertyChanged {

		#region Properties
		public double MaxStraightRange { get; init; }
		[XmlIgnore]
		public const double FuelDecrease = 0.01;

		private double fuel = 100;
		public double Fuel {
			get => fuel;
			set => fuel = Math.Min(100, Math.Max(0, value));
		}

		#region Map
		private Point position;
		public Point Position {
			get => position;
			set {
				position = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
			}
		}
		[XmlIgnore]
		[PropertyTools.DataAnnotations.Browsable(false)]
		public Color Color { get; set; } = Colors.Red;
		[XmlIgnore]
		public double Speed { get; set; }
		[XmlIgnore]
		public List<Point> OpenedPoints {
			get {
				var vs = new List<Point>();
				if (Pathfinder.ActiveExplorer == null) return vs;
				for (int i = 0; i < Pathfinder.ActiveExplorer.OpenedPoints.Count; i++)
					vs.Add(Pathfinder.ActiveExplorer.OpenedPoints[i].Position);
				return vs;
			}
			set {
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OpenedPoints)));
			}
		}
		[PropertyTools.DataAnnotations.Browsable(false)]
		[XmlIgnore]
		public List<Point> ClosedPoints {
			get {
				var vs = new List<Point>();
				if (Pathfinder.ActiveExplorer == null) return vs;
				for (int i = 0; i < Pathfinder.ActiveExplorer.ClosedPoints.Count; i++)
					vs.Add(Pathfinder.ActiveExplorer.ClosedPoints[i].Position);
				return vs;
			}
			set {
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClosedPoints)));
			}
		}
		public AgentStation Home { get; set; }
		#endregion

		#region Brain
		[XmlIgnore]
		private RobotState state;
		[XmlIgnore]
		public virtual RobotState CurrentState {
			get {
				return state;
			}
			set {
				switch (value) {
					case RobotState.Disable:
						break;
					case RobotState.Broken:
						if (Home != null && (TargetPosition - Home.Position).Length > InteractDistance)
							TargetPosition = Home.Position;
						break;
					case RobotState.Ready:
						if (Home != null && (Position - Home.Position).Length < 10)
							Fuel = 100;
						//объект взят
						if (CurrentState == RobotState.Working) {
							AttachedObj.Finished = true;
							AttachedObj.ReservedAgent = null;
							AttachedObj = null;
						} else if (CurrentState == RobotState.Thinking) {

						} else if (CurrentState == RobotState.Disable || CurrentState == RobotState.Broken) {
							//робот сломался/выключился
							BlockedTargets.Clear();
						}
						break;
					//нужно рассчитать траекторию
					case RobotState.Thinking:
						//инициализация модуля прокладывания пути
						Pathfinder.SelectExplorer(TargetPosition, Position, CurrentState == RobotState.Ready ? InteractDistance : Speed);
						break;
					case RobotState.Going:
						if (CurrentState == RobotState.Thinking && AttachedObj != null) {

						}
						var vs = new List<Point>(Trajectory); vs.Reverse();
						BackTrajectory = vs.ToArray();
						break;
					case RobotState.Working:
						if (CurrentState == RobotState.Thinking)
							Trajectory.Add(Trajectory[^1]);
						break;
					default:
						break;
				}
				state = value;
			}
		}

		#region Trajectory
		[XmlIgnore]
		private List<Point> trajectory = new List<Point>();
		[XmlIgnore]
		[PropertyTools.DataAnnotations.Browsable(false)]
		public PathFinder Pathfinder { get; set; }
		[PropertyTools.DataAnnotations.Browsable(false)]
		[XmlIgnore]
		public Point[] BackTrajectory { get; set; }
		[XmlIgnore]
		public List<Point> Trajectory {
			get { return trajectory; }
			set {
				trajectory = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Trajectory)));
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TraversedWay)));
			}
		}
		[XmlIgnore]
		public Point TargetPosition {
			get {
				return Trajectory.Count > 0 ? Trajectory[^1] : Position; //последняя точка пути
			}
			set {
				Trajectory.Clear();
				Trajectory.Add(value);
				CurrentState = RobotState.Thinking;
			}
		}
		#endregion

		#region Interact
		[XmlIgnore]
		public int InteractDistance { get; init; } = 30;
		[XmlIgnore]
		public int ViewingDistance { get; init; } = 2;
		[XmlIgnore]
		[PropertyTools.DataAnnotations.Browsable(false)]
		public Target? AttachedObj { get; set; } = null;
		[XmlIgnore]
		public List<Target> BlockedTargets { get; set; } = new List<Target>();
		[XmlIgnore]
		public List<Agent> OtherAgents { get; set; } = new List<Agent>();
		#endregion

		#region Debug Info
		[XmlIgnore]
		public long ThinkingIterations { get; protected set; } = 0;
		[XmlIgnore]
		public long WayIterations { get; protected set; } = 0;
		[XmlIgnore]
		public double TraversedWay { get; set; } = 0;
		[XmlIgnore]
		public double DistanceToTarget {
			get {
				if (Trajectory.Count < 1 || AttachedObj == null) return -1;
				if (Trajectory.Count == 1) return PathFinder.Distance(Position, Trajectory[0]);

				double s = PathFinder.Distance(Position, Trajectory[0]);
				for (int i = 0; i < Trajectory.Count - 1; i++)
					s += PathFinder.Distance(Trajectory[i], Trajectory[i + 1]);
				s += PathFinder.Distance(trajectory[^1], AttachedObj.Position);
				return s;
			}
		}
		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion

		#endregion

		#region Drawing
		public virtual UIElement Build() {
			Ellipse el = new Ellipse();
			el.Width = 20;
			el.Height = 20;
			el.Fill = new SolidColorBrush(Color);
			el.Stroke = Brushes.Black;
			el.StrokeThickness = 2;
			el.Margin = new Thickness(-10, -10, 0, 0);
			System.Windows.Controls.Panel.SetZIndex(el, 3);

			Binding binding = new Binding(nameof(Position) + ".X");
			binding.Source = this;
			el.SetBinding(System.Windows.Controls.Canvas.LeftProperty, binding);
			binding = new Binding(nameof(Position) + ".Y");
			binding.Source = this;
			el.SetBinding(System.Windows.Controls.Canvas.TopProperty, binding);

			return el;
		}
		public virtual UIElement BuildTrajectory() {
			Polyline polyline = new Polyline();
			polyline.Stroke = new SolidColorBrush(Colors.Gray);
			polyline.StrokeThickness = 2;
			polyline.StrokeDashArray = new DoubleCollection(new double[] { 4.0, 2.0 });
			polyline.StrokeDashCap = PenLineCap.Round;
			polyline.Margin = new Thickness(0, 0, 0, 0);

			Binding b = new Binding(nameof(Trajectory));
			b.Source = this;
			b.Converter = new TrajectoryConverter();
			polyline.SetBinding(Polyline.PointsProperty, b);
			return polyline;
		}
		public virtual UIElement PointsAnalyzed(bool opened) {
			Polyline polyline = new Polyline();
			polyline.Stroke = new SolidColorBrush(opened ? Colors.LightGreen : Colors.DarkRed);
			polyline.StrokeThickness = 5.0;
			/*polyline.StrokeDashArray = new DoubleCollection(new double[] { MaxStraightRange });
            polyline.StrokeStartLineCap = PenLineCap.Round;
            polyline.StrokeDashCap = PenLineCap.Round;
            polyline.StrokeEndLineCap = PenLineCap.Round;*/
			polyline.Margin = new Thickness(-5, -5, 0, 0);

			Binding b = new Binding(opened ? nameof(OpenedPoints) : nameof(ClosedPoints));
			b.Source = this;
			b.Converter = new TrajectoryConverter();
			polyline.SetBinding(Polyline.PointsProperty, b);
			return polyline;
		}
		#endregion

		#region Constructors
		public Agent(Point pos) : this() {
			Position = pos;
		}
		public Agent(int X, int Y) : this(new Point(X, Y)) { }
		public Agent() {
			Color = Colors.Red;
			CurrentState = RobotState.Ready;
			AttachedObj = null;
			Speed = 5F;
			InteractDistance = 30;
			BlockedTargets = new List<Target>();
			MaxStraightRange = 2 * Speed;
		}
		#endregion

		public virtual void Simulate() {
			Fuel -= FuelDecrease;
			switch (CurrentState) {
				case RobotState.Ready:
					if (Home != null && (Home.Position - Position).Length > 10 && TargetPosition != Home.Position)
						TargetPosition = Home.Position;
					else if ((Position - Home.Position).Length < 10)
						Fuel = 100;
					break;
				case RobotState.Thinking:
					if (AttachedObj != null && AttachedObj.ReservedAgent != null && OtherAgents.Contains(AttachedObj.ReservedAgent)) {
						CurrentState = RobotState.Ready;
						break;
					}
					Trajectory = Pathfinder.Result;
					//ошибка при расчётах
					if (Pathfinder.IsCompleted && Pathfinder.Result == null) {
						if (AttachedObj != null)
							BlockedTargets.Add(AttachedObj);
						AttachedObj = null;
					} else if (Pathfinder.IsCompleted) {
						//путь найден
						Pathfinder.IsCompleted = false;
						//робот едет к объекту
						if (AttachedObj == null || AttachedObj != null && AttachedObj.ReservedAgent != this)
							CurrentState = RobotState.Going;
						//робот доставляет объект
						else if (AttachedObj.ReservedAgent == this) {
							CurrentState = RobotState.Working;
						}
						//переключение на другую задачу
						else
							CurrentState = RobotState.Ready;
						ThinkingIterations += Pathfinder.Iterations;
					} else
						Pathfinder.NextStep(); //продолжение расчёта
					break;
			}
		}
		protected virtual void Move() {
			Point nextPoint = Trajectory[0];

			if (PathFinder.Distance(Position, nextPoint) < MaxStraightRange) {
				List<Point> pc = new(Trajectory.Skip(1));
				if (pc.Any()) {
					TraversedWay += PathFinder.Distance(nextPoint, pc[0]);
					nextPoint = pc[0];
				}
				Trajectory = pc;
			}
			Vector V = nextPoint - Position;
			if (V.Length > 0)
				V.Normalize();
			//новое значение
			Position = new Point(Position.X + V.X * Speed, Position.Y + V.Y * Speed);
			WayIterations++;
		}
		public override string ToString() {
			return Enum.GetName(typeof(RobotState), state) + "_" +
				new Point(Math.Round(position.X, 2), Math.Round(position.Y, 2)).ToString();
		}
	}
}
