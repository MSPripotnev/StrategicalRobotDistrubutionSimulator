using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SRDS.Model.Map.Stations;

using System.ComponentModel;
using System.Windows.Controls;
using System.Xml.Serialization;

using SRDS.Model;
using SRDS.Model.Environment;

public class Meteostation : Station, IPlaceableWithArea, ITimeSimulatable {
    public new event PropertyChangedEventHandler? PropertyChanged;
    private readonly Stack<double> temperatures = new Stack<double>(),
                          humidities = new Stack<double>(),
                          pressures = new Stack<double>();
    public const double WorkRadius = 800.0;
    [Category(nameof(Temperature))]
    public double Temperature { get; set; }
    [XmlIgnore]
    [Category(nameof(Temperature))]
    public double TemperatureChange { get; private set; }
    [Category(nameof(Humidity))]
    public double Humidity { get; set; }
    [XmlIgnore]
    [Category(nameof(Humidity))]
    public double HumidityChange { get; private set; }
    [XmlIgnore]
    [Category(nameof(Pressure))]
    public double Pressure { get; set; }
    [XmlIgnore]
    [Category(nameof(Pressure))]
    public double PressureChange { get; private set; }
    [XmlIgnore]
    [Category("Wind")]
    public double WindSpeed { get; private set; }
    /// <summary>
    /// Summary clouds fallout in mm/h = (kg/m^2)/h
    /// </summary>
    [XmlIgnore]
    [Category("Wind")]
    public WindDirectionType WindDirection { get; private set; }
    [Category("Clouds")]
    public double PrecipitationIntensity { get; set; }
    [XmlIgnore]
    [Category("Clouds")]
    public bool PrecipitationTypeIsRain { get => PrecipitationIntensity > 0 && Temperature >= 0 && Humidity > 60; }
    [Category("Clouds")]
    public Cloudness CloudnessType { get; set; }
    private readonly Random rnd;
    private TimeSpan timeFlow = TimeSpan.Zero;
    private DateTime _time;
    private DateTime Ctime {
        set {
            timeFlow = value - _time;
            _time = value;
        }
    }
    public Meteostation() {
        rnd = new Random((int)DateTime.Now.Ticks);
        Color = Colors.LightGray;
        bitmapImage = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("../../../Model/Map/Stations/meteostation.png", UriKind.Relative));
    }
    public Meteostation(Point pos) : this() {
        Position = pos;
    }
    public void Simulate(object? sender, DateTime time) {
        if (sender is not GlobalMeteo meteo)
            return;
        Ctime = time;

        if (time.Second == 0) {
            Temperature = Math.Round(meteo.Temperature + (rnd.NextDouble() - 0.5) / 10, 1);
            Humidity = Math.Round(meteo.Humidity + (rnd.NextDouble() - 0.5) / 10);
            Pressure = Math.Round(meteo.Pressure + rnd.NextDouble() - 0.5);
        }

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

        PropertyChanged?.Invoke(this, new(nameof(Temperature)));
        PropertyChanged?.Invoke(this, new(nameof(Humidity)));
        PropertyChanged?.Invoke(this, new(nameof(Pressure)));
        PropertyChanged?.Invoke(this, new(nameof(TemperatureChange)));
        PropertyChanged?.Invoke(this, new(nameof(HumidityChange)));
        PropertyChanged?.Invoke(this, new(nameof(PressureChange)));

        PrecipitationIntensity = 0;
        double cloudness_area = 0;
        foreach (var o in meteo.CloudControl.Clouds.Where(p => p.Length/2 + WorkRadius > (p.Position - Position).Length ||
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
        PrecipitationIntensity = Math.Round(PrecipitationIntensity * 3600, 4);
        PropertyChanged?.Invoke(this, new(nameof(PrecipitationIntensity)));
        PropertyChanged?.Invoke(this, new(nameof(CloudnessType)));

        WindSpeed = meteo.Wind.Length / timeFlow.TotalSeconds;
        WindDirection = GlobalMeteo.GetWindDirection(meteo.Wind);
        PropertyChanged?.Invoke(this, new(nameof(WindSpeed)));
        PropertyChanged?.Invoke(this, new(nameof(WindDirection)));
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
        Canvas.SetZIndex(workZone, 0);
        return workZone;
    }
}
