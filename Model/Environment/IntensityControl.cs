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
public struct IntensityCell {
    public double Snow { get; set; } = 0;
    public double MashPercent { get; set; } = 0;
    public IntensityCell(double snow, double mashPercent) {
        Snow = snow;
        MashPercent = mashPercent;
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
public class IntensityControl : INotifyPropertyChanged {
    #region IntensityMap
    [XmlArray]
    public IntensityCell[][]? IntensityMap { get; set; }
    public const int IntensityMapScale = 20;
    [XmlIgnore]
    private Size Borders { get; init; }
    public event PropertyChangedEventHandler? PropertyChanged;
    private UIElement[][]? intensityMapUI;
    [XmlIgnore]
    public UIElement[][]? IntensityMapUI {
        get {
            if (intensityMapUI is null || intensityMapUI[0] is null) {
                int wsize = (int)Math.Ceiling(Borders.Width / IntensityMapScale), hsize = (int)Math.Ceiling(Borders.Height / IntensityMapScale);
                IntensityMap = new IntensityCell[wsize][];
                intensityMapUI = new UIElement[wsize][];
                for (int i = 0; i < wsize; i++) {
                    IntensityMap[i] = new IntensityCell[hsize];
                    intensityMapUI[i] = new UIElement[hsize];
                    for (int j = 0; j < hsize; j++) {
                        var converter = new IntensityMapConverter();
                        Rectangle el = new Rectangle() {
                            Width = IntensityMapScale,
                            Height = IntensityMapScale,
                            RadiusX = IntensityMapScale / 4,
                            RadiusY = IntensityMapScale / 4,
                            Opacity = 0.25,
                            Margin = new Thickness(i * IntensityMapScale, j * IntensityMapScale, 0, 0),
                            Fill = (Brush)converter.Convert(IntensityMap[i][j], typeof(Color), i, CultureInfo.CurrentCulture),
                            Uid = $"{nameof(IntensityMap)}[{i}][{j}]",
                        };
                        Binding b = new Binding($"{nameof(IntensityMap)}[{i}][{j}].{nameof(IntensityCell.Snow)}");
                        b.Source = this;
                        b.Converter = new IntensityMapConverter();
                        el.SetBinding(Rectangle.FillProperty, b);
                        intensityMapUI[i][j] = el;
                    }
                }
            }
            return intensityMapUI;
        }
    }

    public IntensityControl(Size borders) {
        Borders = borders;
        IntensityMap = Array.Empty<IntensityCell[]>();
    }
    public void GenerateIntensity(SnowCloud[] clouds, Obstacle[] obstacles, TimeSpan timeFlow) {
        if (!(IntensityMap is not null && IntensityMap.Any())) return;
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
                            !Obstacle.IsPointOnAnyObstacle(pos, obstacles, ref iter))
                        IntensityMap[i][j] += Math.Min(cloud.Intensity * Math.Sqrt(cloud.Width * cloud.Length) / p.Length * timeFlow.TotalMinutes, 1e4);
                }
            }
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntensityMap)));
    }
    public static (int i, int j) GetPointIntensityIndex(Point pos) =>
            ((int)Math.Round(pos.X / IntensityMapScale), (int)Math.Round(pos.Y / IntensityMapScale));
    public static Point GetIntensityMapPoint(int i, int j) =>
            new Point(i * IntensityMapScale, j * IntensityMapScale);
    #endregion
}
