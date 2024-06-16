using SRDS.Map.Targets;

using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace SRDS.Agents.Drones {
	public enum SnowRemoverType {
		PlowBrush,
		Shovel,
		Rotor,
		Cleaver,
		AntiIceDistributor
	}
	public class SnowRemover : Agent {
		[XmlIgnore]
		public double RemoveSpeed { get; private set; }
		[XmlIgnore]
		public double MashSpeed { get; private set; }
		private SnowRemoverType[] devices;
		[XmlArray("Devices")]
		[XmlArrayItem("Device")]
		public SnowRemoverType[] Devices {
			get => devices;
			set {
				devices = value;
				if (Devices.Contains(SnowRemoverType.Rotor)) {
					RemoveSpeed = 1.0;
					Color = Colors.SkyBlue;
				} else if (Devices.Contains(SnowRemoverType.PlowBrush)) {
					RemoveSpeed = 0.8;
					Color = Colors.Silver;
				} else if (Devices.Contains(SnowRemoverType.Shovel)) {
					RemoveSpeed = 0.5;
					Color = Colors.SlateGray;
				} else RemoveSpeed = 0;
				if (Devices.Contains(SnowRemoverType.Cleaver)) {
					MashSpeed = 1.0;
					Color = Colors.Beige;
				} else if (Devices.Contains(SnowRemoverType.AntiIceDistributor)) {
					MashSpeed = 2.0;
					Color = Colors.Aqua;
				} else MashSpeed = 0;
			}
		}
		public SnowRemover(Point position, SnowRemoverType[] devices) : this() {
			Position = position;
			Devices = devices;
		}
		public SnowRemover() : base() {
			InteractDistance = 8;
		}
		public override UIElement Build() {
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
		public override void Simulate() {
			Fuel -= FuelDecrease;
			switch (CurrentState) {
				case RobotState.Broken:
				case RobotState.Ready:
					base.Simulate();
					break;
				case RobotState.Going:
					//есть куда двигаться
					if (Trajectory.Count > 0) {
						//двигаемся
						Move();
						//дошли до нужной точки
						if (PathFinder.Distance(Position, TargetPosition) <= InteractDistance) {
							//цель = объект
							if (AttachedObj != null)
								//захватываем объект
								CurrentState = RobotState.Working;
							else
								CurrentState = RobotState.Ready;
						}
					}
					if (AttachedObj != null && (AttachedObj.Position - Position).Length < InteractDistance)
						CurrentState = RobotState.Working;
					break;
				case RobotState.Working: {
					if (Fuel < (Position - Home.Position).Length / Speed * FuelDecrease)
						CurrentState = RobotState.Broken;
					Snowdrift s = AttachedObj as Snowdrift;
					s.Level -= RemoveSpeed * s.MashPercent * s.MashPercent / 10000.0;
					s.MashPercent += MashSpeed;

					if (s.Level <= 0)
						CurrentState = RobotState.Ready;
					break;
				}
				case RobotState.Thinking:
					base.Simulate();
					break;
			}
		}
	}
}
