using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Serialization;

using SRDS.Agents;
using SRDS.Direct;
using SRDS.Direct.Qualifiers;
using SRDS.Map;
using SRDS.Map.Targets;

namespace SRDS.Analyzing.Models;
public class ParametrRange {
    public bool IsConst { get; init; }
    private List<double> Values;

    public ParametrRange((double start, double end, double step) range) {
        if (IsConst = range.step == 0) {
            Values = Enumerable.Repeat(Math.Round(range.start, 15), (int)range.end).ToList();
        }
        else {
            Values = new List<double>((int)(range.end - range.start + 1));
            for (double i = Math.Round(range.start, 15); i <= range.end; i += range.step)
                Values.Add(Math.Round(i, 15));
        }
    }
    public static implicit operator List<double>(ParametrRange range) {
        return range.Values;
    }
    public static implicit operator List<float>(ParametrRange range) {
        return range.Values.ConvertAll(p => (float)Math.Round(p, 15));
    }
    public static implicit operator List<int>(ParametrRange range) {
        return range.Values.ConvertAll(p => (int)Math.Round(p));
    }
}
public class ParametrRangeGeneratedModel : IModel {
    [XmlAttribute("name")]
    public string Name { get; set; }
    [XmlIgnore]
    public string Path { get; set; }
    public string Map { get; set; }
    public int MaxAttempts { get; set; } = Testing.Default.AttemptsMax;
    [XmlArray("Transporters")]
    [XmlArrayItem("TransporterCount")]
    public List<int> TransportersT { get; set; } = new List<int>();
    [XmlArray("Scales")]
    [XmlArrayItem("Scale")]
    public List<float> ScalesT { get; set; } = new List<float>();
    [XmlArray("Targets")]
    [XmlArrayItem("Target")]
    public List<Target> TargetsT { get; set; } = new List<Target>();
    public ParametrRangeGeneratedModel() {
        TransportersT = new List<int>();
        ScalesT = new List<float>();
        TargetsT = new List<Target>();
    }
    public ParametrRangeGeneratedModel(string name, string map, int targetsCount, (int, int, int) transporterRange, (float, float, float) scaleRange) {
        Name = name;
        Map = System.IO.Path.Combine(Paths.Default.Maps, map);

        TacticalMap tmap = new TacticalMap(Map);
        TargetsT = FillTargetsTByPerimetr(targetsCount, tmap.Obstacles);

        TransportersT = new ParametrRange(transporterRange);
        ScalesT = new ParametrRange(scaleRange);
        if (!TransportersT.Any()) {
            transporterRange.Item2 = ScalesT.Count;
            TransportersT = new ParametrRange(transporterRange);
        }
        if (!ScalesT.Any()) {
            scaleRange.Item2 = TransportersT.Count;
            ScalesT = new ParametrRange(scaleRange);
        }
        Path = System.IO.Path.Combine(Paths.Default.Tests, "Complete", $"{Name}.xml");
        using (FileStream fs = new FileStream(Path, FileMode.Create)) {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(ParametrRangeGeneratedModel));
            XmlWriterSettings settings = new XmlWriterSettings()
            {
                Indent = true,
                CloseOutput = true,
            };
            xmlSerializer.Serialize(fs, this);
        }
    }
    public ParametrRangeGeneratedModel(string path) {
        path = System.IO.Path.Combine(Paths.Default.Tests, path);
        if (File.Exists(path))
            using (FileStream fs = new FileStream(path, FileMode.Open))
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(ParametrRangeGeneratedModel));
                ParametrRangeGeneratedModel m = (ParametrRangeGeneratedModel)xmlSerializer.Deserialize(fs);
                Map = m.Map;
                TransportersT = m.TransportersT;
                TargetsT = m.TargetsT;
                ScalesT = m.ScalesT;
                Name = m.Name;
                Path = path;
            }
        else MessageBox.Show("�� ������� ����� ����: " + path);
    }
    public Director Unpack() {
        var res = new Director() {
            MapPath = Map,
            Scale = ScalesT[^1],
#if METEO
			    Meteo = new GlobalMeteo(Map);
#endif
        };
        for (int i = 0; i < TransportersT[^1]; i++) {
            var t = new Agents.Drones.Transporter(res.Map.Stations[0].Position);
            t.Speed = ScalesT[^1];
            res.Add(t);
        }

        for (int i = 0; i < TargetsT.Count; i++)
            res.Add(new Crop(TargetsT[i].Position));

        return res;
    }

    private List<Target> FillTargetsTByPerimetr(int targetsCount, Obstacle[] obstacles) {
        List<Target> targets = new List<Target>();
        int targetsPerField = targetsCount / obstacles.Length; //recalc foreach by perimetr
        int remainTargetsPerField = targetsCount % obstacles.Length;
        if (targetsPerField < 0) return null;
        for (int fi = 0; fi < obstacles.Length; fi++) {
            Polygon polygon = obstacles[fi].Polygon;
            Point centerP = new Point((polygon.Points.MaxBy(p => p.X).X + polygon.Points.MinBy(p => p.X).X) / 2,
                (polygon.Points.MaxBy(p => p.Y).Y + polygon.Points.MinBy(p => p.Y).Y) / 2);
            Size polygonSize = new Size(polygon.Points.MaxBy(p => p.X).X - polygon.Points.MinBy(p => p.X).X,
                polygon.Points.MaxBy(p => p.Y).Y - polygon.Points.MinBy(p => p.Y).Y);

            double marginPerTarget;
            if (polygonSize.Width / polygonSize.Height < 2.5 && 1 / polygonSize.Width * polygonSize.Height < 2.5) {
                polygon.RenderTransform = new ScaleTransform(1 - 1.0 / 4 * (polygonSize.Height / polygonSize.Width), 1 - 1.0 / 4,
                    centerP.X, centerP.Y);
                var transformedPoints = new Point[polygon.Points.Count + 1];
                for (int k = 0; k < polygon.Points.Count; k++)
                    transformedPoints[k] = polygon.RenderTransform.Value.Transform(polygon.Points[k]);
                transformedPoints[^1] = transformedPoints[0];
                Obstacle field = new Obstacle(transformedPoints);
                double temp;
                marginPerTarget = field.Perimetr() / targetsPerField;
                temp = marginPerTarget;
                Vector V;
                for (int bi = 0; bi < field.Borders.Length - 1; bi++) {
                    V = field.Borders[bi + 1] - field.Borders[bi]; //������ �����������
                    if (V.Length + temp < marginPerTarget) {
                        temp += V.Length;
                        continue;
                    }
                    V.Normalize();
                    var pos = field.Borders[bi] + V * (marginPerTarget - temp);
                    targets.Add(new Crop(pos));
                    temp = 0;
                    for (int i = 1; ; i++) {
                        if (obstacles[fi].PointOnObstacle(pos + V * marginPerTarget * i))
                            targets.Add(new Crop(pos + V * marginPerTarget * i));
                        if ((field.Borders[bi + 1] - pos - V * marginPerTarget * i).Length <= marginPerTarget) {
                            temp += (field.Borders[bi + 1] - pos - V * marginPerTarget * i).Length;
                            break;
                        }
                    }
                }
            } else {
                double lineD;
                if (targetsPerField % 2 == 1)
                    targets.Add(new Crop(centerP));
                if (polygonSize.Width > polygonSize.Height) {
                    lineD = obstacles[fi].Borders.MaxBy(p => p.X).X - obstacles[fi].Borders.MinBy(p => p.X).X;
                    marginPerTarget = lineD * 0.9 / targetsPerField;
                    for (int i = 0; i < targetsPerField / 2; i++) {
                        targets.Add(new Crop(new Point(centerP.X + marginPerTarget * (i + 1) - marginPerTarget / 2 * (1 - targetsPerField % 2), centerP.Y)));
                        targets.Add(new Crop(new Point(centerP.X - marginPerTarget * (i + 1) + marginPerTarget / 2 * (1 - targetsPerField % 2), centerP.Y)));
                    }
                } else {
                    lineD = obstacles[fi].Borders.MaxBy(p => p.Y).Y - obstacles[fi].Borders.MinBy(p => p.Y).Y;
                    marginPerTarget = lineD * 0.9 / targetsPerField;
                    for (int i = 0; i < targetsPerField / 2; i++) {
                        targets.Add(new Crop(new Point(centerP.X, centerP.Y + marginPerTarget * (i + 1) - marginPerTarget / 2 * (1 - targetsPerField % 2))));
                        targets.Add(new Crop(new Point(centerP.X, centerP.Y - marginPerTarget * (i + 1) + marginPerTarget / 2 * (1 - targetsPerField % 2))));
                    }
                }
            }
        }
        return targets;
    }
}
