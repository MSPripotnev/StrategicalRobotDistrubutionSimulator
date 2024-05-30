using SRDS.Environment;

using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SRDS.Map.Targets {
	public class Snowdrift : Target {
		private Color SnowColor(SnowType type) => type switch {
			SnowType.LooseSnow => Colors.Snow,
			SnowType.Snowfall => Colors.PeachPuff,
			SnowType.IceSlick => Colors.AliceBlue,
			SnowType.BlackIce => Colors.DarkGray,
			SnowType.Icy => Colors.LightSkyBlue
		};
		public SnowType Type { get; init; }
		private double level;
		public double Level {
			get => level;
			set {
				Math.Max(0, level = value);
			}
		}
		private double mash = 100;
		public double MashPercent {
			get => mash;
			set => mash = Math.Max(0, Math.Min(100, value));
		}
		public Snowdrift(Point pos, double level) : base(pos) {
			Type = (SnowType)(new Random().Next(0, Enum.GetValues(typeof(SnowType)).Length-1));
			Level = level;
			switch (this.Type) {
				case SnowType.Snowfall:
					MashPercent = 95;
					break;
				case SnowType.IceSlick:
					MashPercent = 80;
					break;
				case SnowType.BlackIce:
					MashPercent = 50;
					break;
				case SnowType.Icy:
					MashPercent = 0;
					break;
			}
			Color = SnowColor(Type);
		}
		public Snowdrift() : this(new Point(0,0), 0) { }
		public override UIElement Build() {
			Random rnd = new Random();
			int msize = (int)Math.Round(Level);
			Ellipse el = new Ellipse() {
				Width = msize,
				Height = msize,
				Fill = new SolidColorBrush(Color),
				Stroke = Brushes.Black,
				StrokeThickness = 1,
			};
			el.Margin = new Thickness(-el.Width/2, -el.Height/2, 0, 0);
			el.RenderTransform = new RotateTransform(rnd.NextDouble() * 2*Math.PI);
			System.Windows.Controls.Panel.SetZIndex(el, 4);

			Binding binding = new Binding(nameof(Position) + ".X");
			binding.Source = this;
			el.SetBinding(System.Windows.Controls.Canvas.LeftProperty, binding);
			binding = new Binding(nameof(Position) + ".Y");
			binding.Source = this;
			el.SetBinding(System.Windows.Controls.Canvas.TopProperty, binding);

			return el;
		}
	}
}
