using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;

using SRDS.Model.Map;
using System.Xml.Serialization;

namespace SRDS.Model.Environment;
public class IntensityMapConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is not double density || density < 0)
            return new RadialGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(100, 255, 255, 255));

        density = density / 10 * 256;
        byte r = (byte)Math.Min(255, Math.Round(density)),
             b = (byte)Math.Max(0, 255 - Math.Round(density));
        return new RadialGradientBrush(Color.FromArgb((byte)(Math.Abs(r - b) / 3), r, 0, b), Color.FromArgb((byte)(Math.Abs(r - b) / 5), 0, 0, 0));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return DependencyProperty.UnsetValue;
    }
}
public class IntensityCell : INotifyPropertyChanged {
    public event PropertyChangedEventHandler? PropertyChanged = default;
    private double snow = 0, icyPercent = 0;
    public double Snow {
        get => snow;
        set {
            snow = Math.Max(Math.Min(value, 1e4), 0);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Snow)));
        }
    }
    public double IcyPercent {
        get => icyPercent;
        set {
            icyPercent = Math.Max(0, Math.Min(value, 100));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IcyPercent)));
        }
    }
    readonly int i, j;
    public IntensityCell(double _snow, double _icyPercent, int i, int j) {
        snow = _snow;
        icyPercent = _icyPercent;
        this.i = i; this.j = j;
    }
    private UIElement? ui = null;
    public UIElement? UI {
        get {
            if (ui is not null) return ui;
            var converter = new IntensityMapConverter();
            Rectangle el = new Rectangle() {
                Width = IntensityControl.IntensityMapScale,
                Height = IntensityControl.IntensityMapScale,
                RadiusX = IntensityControl.IntensityMapScale / 4,
                RadiusY = IntensityControl.IntensityMapScale / 4,
                Opacity = 0.25,
                Margin = new Thickness(i * IntensityControl.IntensityMapScale, j * IntensityControl.IntensityMapScale, 0, 0),
                Fill = (Brush)converter.Convert(Snow, typeof(Color), i, CultureInfo.CurrentCulture),
                Uid = $"{nameof(IntensityCell)}[{i}][{j}]",
            };
            Binding b = new Binding($"{nameof(Snow)}");
            b.Source = this;
            b.Converter = new IntensityMapConverter();
            el.SetBinding(Rectangle.FillProperty, b);
            return ui = el;
        }
    }
    private double deicing = 0;
    public double Deicing {
        get => deicing;
        set {
            deicing = Math.Max(0, Math.Min(value, 130));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Deicing)));
        }
    }
    public static double operator^(IntensityCell cell, double icy) =>
        Math.Max(0, Math.Min((cell.IcyPercent + icy) / 2, 100));
}
public class IntensityControl {
    #region IntensityMap
    [XmlArray]
    public IntensityCell?[][] IntensityMap { get; set; }
    public const int IntensityMapScale = 25;
    [XmlIgnore]
    private Size Borders { get; init; }

    public IntensityControl(TacticalMap map) {
        Borders = map.Borders;
        IntensityMap = Array.Empty<IntensityCell[]>();
        if (Borders.Width > 0 && Borders.Height > 0) {
            int wsize = (int)Math.Ceiling(Borders.Width / IntensityMapScale), hsize = (int)Math.Ceiling(Borders.Height / IntensityMapScale);
            IntensityMap = new IntensityCell[wsize][];
            for (int i = 0; i < wsize; i++) {
                IntensityMap[i] = new IntensityCell[hsize];
                for (int j = 0; j < hsize; j++)
                    if (map.Roads.Any(p => 0 < p.DistanceToRoad(GetIntensityMapPoint(i, j)) && p.DistanceToRoad(GetIntensityMapPoint(i, j)) < p.Height * 6))
                        IntensityMap[i][j] = new IntensityCell(0, 0, i, j);
                    else IntensityMap[i][j] = null;
            }
        }
    }
    bool flag = false;
    public void GenerateIntensity(CloudControl cloudControl, Obstacle[] obstacles, TimeSpan timeFlow, Dictionary<SnowType, double> snowTypes) {
        if (!(IntensityMap is not null && IntensityMap.Any())) return;
        if (!cloudControl.Clouds.Any()) return;
        flag = !flag;

        double mid_icy = 0;
        for (int k = 0; k < snowTypes.Count; k++)
            mid_icy += snowTypes[(SnowType)k] * GlobalMeteo.GetIcyPercent((SnowType)k);
        mid_icy /= snowTypes.Count;

        for (int c = flag ? 0 : 1; c < cloudControl.Clouds.Length; c+=2) {
            var cloud = cloudControl.Clouds[c];
            double a = Math.Max(cloud.Width, cloud.Length);
            double point_icy = 0;
            (int cloudStartPosi, int cloudStartPosj) = GetPointIntensityIndex(cloud.Position - new Vector(a / 2, a / 2));
            (int cloudEndPosi, int cloudEndPosj) = GetPointIntensityIndex(cloud.Position + new Vector(a / 2, a / 2));
            cloudStartPosi = Math.Max(0, cloudStartPosi);
            cloudStartPosj = Math.Max(0, cloudStartPosj);
            cloudEndPosi = Math.Min(cloudEndPosi, IntensityMap.Length);
            cloudEndPosj = Math.Min(cloudEndPosj, IntensityMap[0].Length);
            if (cloudControl.Coverage < 0.4)
                point_icy += 0.2 * GlobalMeteo.GetIcyPercent(SnowType.IceSlick) / snowTypes.Count;

            for (int i = cloudStartPosi; i < cloudEndPosi; i++) {
                for (int j = cloudStartPosj; j < cloudEndPosj; j++) {
                    Point pos = GetIntensityMapPoint(i, j);
                    Vector p = (pos - cloud.Position);
                    long iter = 0;
                    double cloud_angle_r = cloud.Angle / 180 * Math.PI;

                    if ((Math.Pow( (p.X * Math.Cos(cloud_angle_r) + p.Y * Math.Sin(cloud_angle_r)) / cloud.Width, 2) +
                        Math.Pow( (p.X * Math.Sin(cloud_angle_r) - p.Y * Math.Cos(cloud_angle_r)) / cloud.Length, 2)) <= 0.25 &&
                            !Obstacle.IsPointOnAnyObstacle(pos, obstacles, ref iter)) {
                        if (IntensityMap[i][j] is IntensityCell cell) {
                            cell.Snow += Math.Min(2 * cloud.Intensity * Math.Sqrt(cloud.Width * cloud.Length) / p.Length * timeFlow.TotalSeconds, 1e4);
                            cell.IcyPercent = cell ^ (mid_icy + point_icy);
                        }
                    }
                }
            }
        }
    }
    public static (int i, int j) GetPointIntensityIndex(Point pos) =>
            ((int)Math.Round(pos.X / IntensityMapScale), (int)Math.Round(pos.Y / IntensityMapScale));
    public static Point GetIntensityMapPoint(int i, int j) =>
            new Point(i * IntensityMapScale, j * IntensityMapScale);
    #endregion
}
