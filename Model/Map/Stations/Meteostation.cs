using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SRDS.Model.Map.Stations;

using System.Xml.Serialization;

using SRDS.Model;
using SRDS.Model.Environment;

public class Meteostation : Station, IPlaceableWithArea, ITimeSimulatable {
    private Stack<double> temperatures = new Stack<double>(),
                          humidities = new Stack<double>(),
                          pressures = new Stack<double>();
    public const double WorkRadius = 150.0;
    public double Temperature { get; set; }
    [XmlIgnore]
    public double TemperatureChange { get; private set; }
    public double Humidity { get; set; }
    [XmlIgnore]
    public double HumidityChange { get; private set; }
    [XmlIgnore]
    public double Pressure { get; set; }
    [XmlIgnore]
    public double PressureChange { get; private set; }
    [XmlIgnore]
    public double WindSpeed { get; private set; }
    [XmlIgnore]
    public WindDirectionType WindDirection { get; private set; }
    public double PrecipitationIntensity { get; set; }
    [XmlIgnore]
    public bool PrecipitationTypeIsRain { get => PrecipitationIntensity > 0 && Temperature >= 0 && Humidity > 60; }
    public Cloudness CloudnessType { get; set; }
    private Random rnd;
    public Meteostation() {
        rnd = new Random((int)DateTime.Now.Ticks);
        Color = Colors.LightGray;
    }
    public Meteostation(Point pos) : this() {
        Position = pos;
    }
    public void Simulate(object? sender, DateTime time) {
        if (sender is not GlobalMeteo meteo)
            return;

        Temperature = Math.Round(meteo.Temperature + rnd.NextDouble() * 2 - 1, 1);
        Humidity = Math.Round(meteo.Humidity + rnd.NextDouble() * 2 - 1);
        Pressure = Math.Round(meteo.Pressure + rnd.Next(0, 1));

        if (time.AddMinutes(1).Hour > time.Hour && temperatures.Any()) {
            TemperatureChange = Temperature - temperatures.Average();
            HumidityChange = Humidity - humidities.Average();
            PressureChange = Pressure - pressures.Average();
            humidities.Clear();
            temperatures.Clear();
            pressures.Clear();
        }
        pressures.Push(Pressure);
        temperatures.Push(Temperature);
        humidities.Push(Humidity);

        PrecipitationIntensity = 0;
        double cloudness_area = 0;
        foreach (var o in meteo.Clouds.Where(p => p.Length/2 + WorkRadius > (p.Position - Position).Length ||
                p.Width/2 + WorkRadius > (p.Position - Position).Length)) {
            double r1 = o.Length > o.Width ? o.Length / 2 : o.Width / 2,
                   r2 = WorkRadius, distance = (o.Position - Position).Length;
            PrecipitationIntensity += Math.Min(o.Intensity, o.Intensity * Math.Abs(distance - r1) / distance);

            double f1 = 2 * Math.Acos((r1 * r1 - r2 * r2 + distance * distance) / (2 * r1 * distance)),
                   f2 = 2 * Math.Acos((r2 * r2 - r1 * r1 + distance * distance) / (2 * r2 * distance)),
                   s1 = r1 * r1 * (f1 - Math.Sin(f1)) / 2,
                   s2 = r2 * r2 * (f2 - Math.Sin(f2)) / 2;
            if (distance <= Math.Abs(r1 - r2))
                cloudness_area = Math.Max(cloudness_area, Math.PI * (r1 < r2 ? r1 * r1 : r2 * r2));
            if (s1 + s2 is not double.NaN)
                cloudness_area = Math.Max(cloudness_area, s1 + s2);
        }
        cloudness_area = Math.Min(cloudness_area, WorkRadius * WorkRadius * Math.PI) / (WorkRadius * WorkRadius * Math.PI);
        if (cloudness_area < 0.1)
            CloudnessType = Cloudness.Clear;
        else if (cloudness_area < 0.7)
            CloudnessType = Cloudness.PartyCloudy;
        else
            CloudnessType = Cloudness.Cloudy;
        WindSpeed = meteo.Wind.Length;
        WindDirection = GlobalMeteo.GetWindDirection(meteo.Wind);
    }

    public UIElement BuildArea() {
        Ellipse workZone = new Ellipse() {
            Width = 2 * WorkRadius,
            Height = 2 * WorkRadius,
            Stroke = Brushes.LightGreen,
            StrokeThickness = 1,
            Margin = new Thickness(-WorkRadius, -WorkRadius, 0, 0),
        };
        Binding binding = new Binding(nameof(Position) + ".X");
        binding.Source = this;
        workZone.SetBinding(System.Windows.Controls.Canvas.LeftProperty, binding);
        binding = new Binding(nameof(Position) + ".Y");
        binding.Source = this;
        workZone.SetBinding(System.Windows.Controls.Canvas.TopProperty, binding);
        return workZone;
    }
}
