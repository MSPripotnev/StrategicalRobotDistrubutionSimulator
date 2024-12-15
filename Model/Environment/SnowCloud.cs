using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SRDS.Model.Environment;

using System;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

using Map;
public class SnowCloud : IPlaceable, ITimeSimulatable {
    private const double min_intensity = 0.005, mid_intensity = 0.02;
    public SnowCloud() {
        Color = Colors.Black;
        Rotation = new RotateTransform(Angle, Width / 2, Length / 2);
    }
    public SnowCloud(Point p, double w, double l, Vector v, double _intensity, DateTime create, DateTime start, DateTime end, double angle) : this() {
        Angle = angle;
        Position = p;
        Velocity = v;
        MaxWidth = w; // X
        MaxLength = l; // Y
        Width = Length = 0;
        creationTime = create;
        Start = start;
        End = end;
        Intensity = MaxIntensity = _intensity;
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    #region Drawing
    private Point position;
    /// <summary>
    /// Center position
    /// </summary>
    public Point Position {
        get => position;
        set {
            position = value;
            Rotation = new RotateTransform(Angle, Width / 2, Length / 2);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rotation)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UIPosition)));
        }
    }
    /// <summary>
    /// Draw start position offset
    /// </summary>
    public Point UIPosition {
        get => position - new Vector(Width / 2, Length / 2);
    }
    public RotateTransform Rotation { get; set; }
    public double Angle { get; set; } = 0;
    public double MaxLength { get; set; }
    public double MaxWidth { get; set; }
    private double length, width;
    public double Length {
        get => length;
        set {
            length = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Length)));
        }
    }
    public double Width {
        get => width;
        set {
            width = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Width)));
        }
    }
    public Color Color { get; set; }
    [XmlIgnore]
    public UIElement? UI { get; private set; } = null;
    public UIElement Build() {
        if (UI is not null)
            return UI;
        Rectangle el = new Rectangle() {
            Stroke = new SolidColorBrush(Color),
            StrokeThickness = 0,
            StrokeDashOffset = 4,
            StrokeDashArray = new DoubleCollection(new double[] { 4.0, 2.0 }),
            Uid = nameof(SnowCloud),
        };
        el.RenderTransform = Rotation;
        Binding binding = new Binding(nameof(UIPosition) + ".X");
        binding.Source = this;
        el.SetBinding(System.Windows.Controls.Canvas.LeftProperty, binding);
        binding = new Binding(nameof(UIPosition) + ".Y");
        binding.Source = this;
        el.SetBinding(System.Windows.Controls.Canvas.TopProperty, binding);
        binding = new Binding(nameof(Width));
        binding.Source = this;
        el.SetBinding(Canvas.WidthProperty, binding);
        binding = new Binding(nameof(Length));
        binding.Source = this;
        el.SetBinding(Canvas.HeightProperty, binding);

        binding = new Binding(nameof(Rotation));
        binding.Source = this;
        el.SetBinding(UIElement.RenderTransformProperty, binding);
        Panel.SetZIndex(el, -1);

        try {
            BitmapImage? image = null;
            if (Intensity < min_intensity)
                image = new BitmapImage(new Uri(@"../../../Properties/cloud.png", UriKind.Relative));
            else if (Intensity < mid_intensity)
                image = new BitmapImage(new Uri(@"../../../Properties/cloud_mid_rainy.png", UriKind.Relative));
            else
                image = new BitmapImage(new Uri(@"../../../Properties/cloud_rainy.png", UriKind.Relative));
            if (image is not null) {
                el.Fill = new ImageBrush(image);
                el.Fill.Opacity = 0.5;
            }
        } catch { }
        return UI = el;
    }
    #endregion

    #region Behaviour

    #region Properties
    public double MaxIntensity { get; private set; } = 0;
    private double intensity = 0;
    public double Intensity {
        get => intensity;
        set {
            intensity = value;
            if (intensity < min_intensity)
                Color = Colors.LightGray;
            else if (intensity < mid_intensity)
                Color = Colors.Gray;
        }
    }
    public Vector Velocity { get; set; }
    public DateTime Start { get; private set; }
    public DateTime End { get; init; }
    private DateTime creationTime;
    public bool Finished { get; set; }
    #endregion

    public void Simulate(object? sender, DateTime time) {
        if (sender is not GlobalMeteo meteo)
            return;

        Vector rndWind = new Vector((new Random().NextDouble() - 0.5) / 8, (new Random().NextDouble() - 0.5) / 8);
        Velocity = meteo.Wind + rndWind;
        Angle += Math.Cos(Vector.AngleBetween(rndWind, new Vector(1, 0))) / 4;
        Position += Velocity;
        double mins, sizeReduceTime, intensityReduceTime;
        if (time > Start) {
            mins = (End - time).TotalMinutes;
            sizeReduceTime = (End - Start).TotalMinutes / 2;
            intensityReduceTime = (End - Start).TotalMinutes / 4;
        } else {
            mins = (time - creationTime).TotalMinutes;
            sizeReduceTime = (Start - creationTime).TotalMinutes / 2;
            intensityReduceTime = (Start - creationTime).TotalMinutes / 4;
        }
        if (mins < intensityReduceTime) {
            if (Intensity <= MaxIntensity)
                Intensity = Math.Max(0, MaxIntensity / intensityReduceTime * mins);
            else
                Intensity = (Intensity - MaxIntensity) / 1.1 + MaxIntensity;
        } else {
            Intensity = MaxIntensity;
        }
        if (mins < sizeReduceTime) {
            if (Width <= MaxWidth) {
                Width = Math.Max(0, MaxWidth / sizeReduceTime * mins);
                Length = Math.Max(0, MaxLength / sizeReduceTime * mins);
            } else {
                Width = (Width - MaxWidth) / 1.1 + MaxWidth;
                Length = (Length - MaxLength) / 1.1 + MaxLength;
            }
        }
        Finished = Width < 0.1 || Length < 0.1 || End < time;
    }
    public SnowCloud Split(Random rnd, DateTime _time) {
        int direction = rnd.Next(0, 3) * 2 + 1;
        double width = this.Width / rnd.Next(2, 4),
               length = this.Length / rnd.Next(2, 4);
        this.MaxWidth *= (1 - width / this.MaxWidth);
        this.MaxLength *= (1 - length / this.MaxLength);
        this.MaxIntensity /= 2;
        creationTime = _time;
        Start = _time.AddMinutes(rnd.Next(30, 60));

        Point position = new Point(
            Math.Round(this.Position.X + (direction / 3 - 1) * width),
            Math.Round(this.Position.Y + (direction % 3 - 1) * length));

        return new SnowCloud(position, width, length, Velocity, this.MaxIntensity, creationTime, Start, this.End.AddMinutes(rnd.NextDouble() * 60 - 30), this.Angle);
    }
    public bool IsOutside(TacticalMap map) {
        Point[] cloudBorders = {
            UIPosition,
            UIPosition + new Vector(Width, 0),
            UIPosition + new Vector(0, Length),
            UIPosition + new Vector(Width, Length),
        };
        return (cloudBorders.All(p => map.PointOutsideBorders(p)));
    }
    #endregion

    #region Misc
    public bool PointInside(Point p) =>
        (p.X - Position.X) * (p.X - Position.X) / Width / Width +
        (p.Y - Position.Y) * (p.Y - Position.Y) / Length / Length <= 1;
    #endregion

}
