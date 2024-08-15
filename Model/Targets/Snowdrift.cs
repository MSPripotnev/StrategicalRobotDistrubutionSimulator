using SRDS.Model.Environment;

using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SRDS.Model.Targets;
public class Snowdrift : Target {
    private Color SnowColor(SnowType type) => type switch {
        SnowType.LooseSnow => Colors.Snow,
        SnowType.Snowfall => Colors.PeachPuff,
        SnowType.IceSlick => Colors.AliceBlue,
        SnowType.BlackIce => Colors.DarkGray,
        SnowType.Icy => Colors.LightSkyBlue
    };
    public SnowType Type { get; set; }
    private double level;
    public double Level {
        get => level;
        set {
            level = Math.Max(0, value);
        }
    }
    private double mash = 100;
    public double MashPercent {
        get => mash;
        set {
            mash = Math.Max(0, Math.Min(100, value));
            if (mash < 30)
                Type = SnowType.Icy;
            else if (mash < 50)
                Type = SnowType.IceSlick;
            else if (mash < 75)
                Type = SnowType.BlackIce;
            else if (mash < 90)
                Type = SnowType.Snowfall;
            else
                Type = SnowType.LooseSnow;
            Color = SnowColor(Type);
        }
    }
    public Snowdrift(Point pos, double level, Random rnd) : base(pos) {
        MashPercent = rnd.NextDouble() * 100;
        Level = level;
    }
    public Snowdrift() : this(new Point(0, 0), 0, new Random()) { }
    public override UIElement Build() {
        Random rnd = new Random();
        int msize = (int)Math.Round(Level) + 3;
        Ellipse el = new Ellipse() {
            Width = msize,
            Height = msize,
            Fill = new SolidColorBrush(Color),
            Stroke = Brushes.Black,
            StrokeThickness = 1,
        };
        el.Margin = new Thickness(-el.Width / 2, -el.Height / 2, 0, 0);
        el.RenderTransform = new RotateTransform(rnd.NextDouble() * 2 * Math.PI);
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
