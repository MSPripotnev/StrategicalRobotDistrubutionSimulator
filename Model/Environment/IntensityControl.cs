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
    private double snow = 0, mashPercent = 0;
    public double Snow {
        get => snow;
        set {
            snow = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Snow)));
        }
    }
    public double MashPercent {
        get => mashPercent;
        set {
            mashPercent = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MashPercent)));
        }
    }
    readonly int i, j;
    public IntensityCell(double _snow, double _mashPercent, int i, int j) {
        snow = _snow;
        mashPercent = _mashPercent;
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
                Uid = $"{nameof(IntensityCell)}[{i}][{j}].{nameof(Snow)}",
            };
            Binding b = new Binding($"{nameof(Snow)}");
            b.Source = this;
            b.Converter = new IntensityMapConverter();
            el.SetBinding(Rectangle.FillProperty, b);
            return ui = el;
        }
    }
    public static IntensityCell operator+(IntensityCell cell, double snow) {
        cell.Snow += Math.Max(Math.Min(snow, 1e4), 0);
        return cell;
    }
    public static IntensityCell operator^(IntensityCell cell, double mash) {
        cell.MashPercent = Math.Max(0, Math.Min((cell.MashPercent + mash)/2, 100));
        return cell;
    }
}
public class IntensityControl {
    #region IntensityMap
    [XmlArray]
    public IntensityCell[][]? IntensityMap { get; set; }
    public const int IntensityMapScale = 20;
    [XmlIgnore]
    private Size Borders { get; init; }

    public IntensityControl(Size borders) {
        Borders = borders;
        IntensityMap = Array.Empty<IntensityCell[]>();
        if (Borders.Width > 0 && Borders.Height > 0) {
            int wsize = (int)Math.Ceiling(Borders.Width / IntensityMapScale), hsize = (int)Math.Ceiling(Borders.Height / IntensityMapScale);
            IntensityMap = new IntensityCell[wsize][];
            for (int i = 0; i < wsize; i++) {
                IntensityMap[i] = new IntensityCell[hsize];
                for (int j = 0; j < hsize; j++)
                    IntensityMap[i][j] = new IntensityCell(0, 0, i, j);
            }
        }
    }
    public void GenerateIntensity(SnowCloud[] clouds, Obstacle[] obstacles, TimeSpan timeFlow) {
        if (!(IntensityMap is not null && IntensityMap.Any())) return;
        if (!clouds.Any()) return;
        foreach (var cloud in clouds.Where(c => c.Intensity > 0)) {
            (int cloudStartPosi, int cloudStartPosj) = GetPointIntensityIndex(new(cloud.Position.X - cloud.Width / 2, cloud.Position.Y - cloud.Length / 2));
            (int cloudEndPosi, int cloudEndPosj) = GetPointIntensityIndex(new(cloud.Position.X + cloud.Width / 2, cloud.Position.Y + cloud.Length / 2));
            cloudStartPosi = Math.Max(0, cloudStartPosi);
            cloudStartPosj = Math.Max(0, cloudStartPosj);
            cloudEndPosi = Math.Min(cloudEndPosi, IntensityMap.Length);
            cloudEndPosj = Math.Min(cloudEndPosj, IntensityMap[0].Length);

            for (int i = cloudStartPosi; i < cloudEndPosi; i++) {
                for (int j = cloudStartPosj; j < cloudEndPosj; j++) {
                    Point pos = GetIntensityMapPoint(i, j);
                    Vector p = (pos - cloud.Position);
                    long iter = 0;
                    if (p.X * p.X / cloud.Width / cloud.Width * 4 + p.Y * p.Y / cloud.Length / cloud.Length * 4 <= 1 &&
                            !Obstacle.IsPointOnAnyObstacle(pos, obstacles, ref iter)) {
                        IntensityMap[i][j].Snow += Math.Min(cloud.Intensity * Math.Sqrt(cloud.Width * cloud.Length) / p.Length * timeFlow.TotalMinutes, 1e4);
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
