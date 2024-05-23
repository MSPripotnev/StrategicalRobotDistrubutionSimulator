using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Media;
using PropertyTools.Wpf;
using System.ComponentModel;

namespace SRDS.Map {
	using SRDS.Agents;
	using SRDS.Environment;

	public enum RoadType {
		Dirt,
		Gravel,
		Asphalt
	}
	public class Crossroad : IPlaceable {
		private Point position;
		[XmlElement(nameof(Point), ElementName = "Position")]
		public Point Position {
			get { return position; }
			set {
				position = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
			}
		}
		[XmlIgnore]
		public Road[] Roads = Array.Empty<Road>();
		[XmlIgnore]
		public Color Color { get; set; } = Colors.DarkGray;

		public event PropertyChangedEventHandler? PropertyChanged;
		public Crossroad(Point position, Road r1, Road r2) {
			Position = position;
			Roads = new Road[] { r1, r2 };
		}
		public UIElement Build() {
			if (!Roads.Any()) return null;
			Rectangle el = new Rectangle();
			el.Width = el.Height = 10;
			el.Margin = new Thickness(Position.X - el.Width/2, Position.Y - el.Height/2, 0, 0);
			el.Fill = new SolidColorBrush(Colors.DarkSlateGray);
			return el;
		}
		public static bool operator ==(Crossroad left, Crossroad right) {
			return left is not null && right is not null && Math.Abs(left.Position.X - right.Position.X) < 10.0 && Math.Abs(left.Position.Y - right.Position.Y) < 10.0;
		}
		public static bool operator !=(Crossroad left, Crossroad right) {
			return !(left == right);
		}
	}

	public class Road : IPlaceable {
		public RoadType Type { get; set; }
		private Point position;
		[XmlElement(nameof(Point), ElementName = "Position")]
		public Point Position {
			get { return position; }
			set {
				position = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
			}
		}
		private Point endPosition;
		[XmlElement(nameof(Point), ElementName = "EndPosition")]
		public Point EndPosition {
			get { return endPosition; }
			set {
				endPosition = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EndPosition)));
			}
		}
		[XmlIgnore]
		public Color Color { get; set; } = Color.FromRgb(70, 70, 0);

		public event PropertyChangedEventHandler? PropertyChanged;
		public double Length { get => (EndPosition - Position).Length; }
		public int Height { get; private set; }
		private int category;
		[XmlAttribute("Category")]
		public int Category {
			get => category;
			set {
				category = value;
				Height = (category - 1) * 5 + 1;
				Type = (RoadType)category;
			}
		}
		[XmlAttribute("Snowness")]
		public double Snowness { get; set; } = 0;
		[XmlIgnore]
		public List<Road> RoadsConnected { get; set; } = new List<Road>();
		public IDrone[] GetAgentsOnRoad(IPlaceable[] agents) {
			return (IDrone[])agents.Where(a => a is IDrone && DistanceToRoad(a.Position) < Height).ToArray();
		}
		public Road(Point start, Point end, int category, Road[] roads) {
			Position = start;
			EndPosition = end;
			Category = category;
			Connect(roads);
		}
		public Road() { }
		public UIElement Build() {
			Vector v = EndPosition - Position;
			v.Normalize(); v *= Height*2;
			(v.Y, v.X) = (v.X, -v.Y);
			Path p = new Path() {
				Fill = new SolidColorBrush(Color),
				Stroke = new SolidColorBrush(Color),
				StrokeThickness = 4,
				Data = new GeometryGroup() {
					Children = {
						new LineGeometry(Position - v, EndPosition - v),
						new LineGeometry(Position + v, EndPosition + v)
					},
					FillRule = FillRule.EvenOdd
				},
			};
			//l.Height = 5;//(Category - 1) * 5 + 1;
			//l.Margin = new Thickness(Position.X, Position.Y, 0, 0);
			return p;
		}
		public void Connect(Road[] roads) {
			RoadsConnected = new List<Road>();
			for (int i = 0; i < roads.Length; i++)
				if ((roads[i] ^ this).HasValue) {
					if (!roads[i].RoadsConnected.Contains(roads[i]))
						roads[i].RoadsConnected.Add(roads[i]);
					if (!RoadsConnected.Contains(roads[i]))
						RoadsConnected.Add(roads[i]);
				}
		}
		public double DistanceToRoad(Point position) {
			Vector rv = (Vector)this;
			double h = Math.Round(Math.Abs(
				(rv.Y * position.X - rv.X * position.Y + EndPosition.X * Position.Y - EndPosition.Y * Position.X) / rv.Length));
			if (h < 25) {
				double d1 = (Position - position).Length,
					   d2 = (EndPosition - position).Length,
					   L = Math.Sqrt(d1 * d1 - h * h) + Math.Sqrt(d2 * d2 - h * h);
				if (L - 10 >= rv.Length) return -h;
			}
			return h;
		}
		public void Simulate(GlobalMeteo meteo) {
			if (Snowness < 0.01)
				Snowness -= 0.00001;
			if (meteo.Temperature > 0)
				Snowness -= 0.002;
			Snowness = Math.Max(0, Math.Min(Snowness, Height*Length/100));
		}
		/// <summary>
		/// ����������� �����
		/// </summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
		public static Point? operator ^(Road left, Road right) {
			double k1 = ((Vector)(left)).X / ((Vector)(left)).Y,
				   k2 = ((Vector)(right)).X / ((Vector)(right)).Y;
			if (k1 == k2) return null;

			double k = (((Vector)left).X * ((Vector)right).Y - ((Vector)left).Y * ((Vector)right).X),
				   s = ((right.Position.X - left.Position.X) * ((Vector)right).Y -
				(right.Position.Y - left.Position.Y) * ((Vector)right).X) / k,
				   t = ((right.Position.Y - left.Position.Y) * ((Vector)right).X -
				(right.Position.X - left.Position.X) * ((Vector)right).Y) / k;
			if (-1 <= s && s <= 1 && -1 <= t && t <= 1)
				return new Point(left.Position.X + (int)(s * ((Vector)left).X), (int)(left.Position.Y + s * ((Vector)left).Y));
			return null;
		}
		public static explicit operator Vector(Road self) => self.EndPosition - self.Position;
		public static bool operator ==(Road left, Road right) {
			return left is not null && right is not null && Math.Abs(left.Position.X - right.Position.X) < 10.0 && Math.Abs(left.Position.Y - right.Position.Y) < 10.0 &&
				Math.Abs(left.EndPosition.X - right.EndPosition.X) < 10.0 && Math.Abs(left.EndPosition.Y - right.EndPosition.Y) < 10.0; ;
		}
		public static bool operator !=(Road left, Road right) {
			return !(left == right);
		}

		public static double DistanceHardness(RoadType type) => type switch {
			RoadType.Dirt => 3.0,
			RoadType.Gravel => 1.5,
			RoadType.Asphalt => 1.0
		};
	}
}
