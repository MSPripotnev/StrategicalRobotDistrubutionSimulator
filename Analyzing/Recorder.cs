using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace SRDS.Analyzing;
using Direct;
using Direct.Executive;
using Direct.Strategical;
using Direct.Tactical;
using Model.Targets;

public class Recorder : IDisposable {
    public int Epoch { get => SystemQuality.Count; }
    public List<double> SystemQuality { get; init; } = new();
    List<List<DistributionQualifyReading>> qualifyReadings = new();
    [XmlArray(ElementName = "QualifyReadings")]
    [XmlArrayItem(ElementName = "QualifyReading")]
    public DistributionQualifyReading[][] QualifyReadings {
        get {
            return qualifyReadings.Select(p => p.ToArray()).ToArray();
        }
        set {
            qualifyReadings = new List<List<DistributionQualifyReading>>(value.Select(p => p.ToList()));
            SystemQuality.Add(value.Last().Sum(p => (p.TakedLevel - (p.TakedTarget as Snowdrift).Level)));
        }
    }
    List<StrategicSituationReading> strategicReadings = new List<StrategicSituationReading>();
    [XmlArray(ElementName = "StrategyReadings")]
    [XmlArrayItem(ElementName = "StrategyReading")]
    public StrategicSituationReading[] StrategicReadings {
        get {
            return strategicReadings.ToArray();
        }
        set {
            strategicReadings = new List<StrategicSituationReading>(value);
        }
    }
    List<ModelReading> readings = new List<ModelReading>();
    [XmlArray(ElementName = "Readings")]
    [XmlArrayItem(ElementName = "Reading")]
    public ModelReading[] Readings {
        get {
            return readings.ToArray();
        }
        set {
            readings = new List<ModelReading>(value);
        }
    }
    public void OnModelSwitched(object? sender, EventArgs e) {
        if (Readings.Any())
            Save();
        readings.Clear();
    }
    public void Save() {
        string resFileName = Path.Combine(Paths.Default.Results, $"Results_{Readings[0].ModelName}.xml");
        SaveInXMLFile(resFileName);
    }
    public void Backup() {
        if (Readings.Any()) {
            string resFileName = Path.Combine(Paths.Default.Results, $"Results_{Readings[0].ModelName}-backup-" + DateTime.Now.ToShortDateString() + "-" + DateTime.Now.ToLongTimeString().Replace(':', '-') + ".xml");
            SaveInXMLFile(resFileName);
        }
    }
    public void SaveResults(Director director, string modelName, TimeSpan fullTime, ref double iterations) {
        var analyzer = new ModelReading() {
            ModelName = modelName,
            TransportersCount = director.Agents.Length,
            Scale = Math.Round(director.PathScale, 3),
            ThinkingIterations = director.ThinkingIterations,
            WayIterations = director.WayIterations,
            FullTime = fullTime.TotalSeconds,
            DistributeIterations = Math.Round(iterations),
            TraversedWayPx = director.TraversedWaySum,
            TraversedWay = Math.Round(director.TraversedWaySum * Testing.Default.K_s, 14),
            STransporterWay = new double[director.Agents.Length],
            TargetsCount = director.Targets.Length
        };
        if (director.Agents.Any()) {
            analyzer.TransportersSpeed = Math.Round(director.Agents[0].Speed, 8) * Testing.Default.K_v;
            for (int i = 0; i < director.Agents.Length; i++) {
                analyzer.STransporterWay[i] = director.Agents[i].TraversedWay;
                analyzer.WorkTimeIt = (uint)Math.Round(Math.Max(analyzer.WorkTimeIt, analyzer.STransporterWay[i]));
            }
            analyzer.WayTime = Math.Round(Testing.Default.K_v / Testing.Default.K_s * analyzer.WayIterations, 14);
        }
        readings.Add(analyzer);
        var vs = QualifyReadings.ToList();
        vs.Add(director.Distributor.DistributionQualifyReadings.Values.ToArray());
        QualifyReadings = vs.ToArray();
        iterations = 0;
    }
    private void SaveInXMLFile(string resFileName) {
        XmlSerializer serializer = new XmlSerializer(typeof(ModelReading));
        if (!File.Exists(resFileName))
            using (FileStream fs = new FileStream(resFileName, FileMode.Create)) {
                XmlWriterSettings settings = new XmlWriterSettings() {
                    Indent = true,
                    ConformanceLevel = ConformanceLevel.Auto,
                    WriteEndDocumentOnClose = false
                };
                var writer = XmlWriter.Create(fs, settings);
                writer.WriteStartDocument();
                writer.WriteStartElement("Readings");
                writer.Close();
            }
        for (int i = 0; i < Readings.Length; i++)
            using (FileStream fs = new FileStream(resFileName, FileMode.Append)) {
                XmlWriterSettings settings = new XmlWriterSettings {
                    OmitXmlDeclaration = true,
                    Indent = true,
                    CloseOutput = true,
                    IndentChars = "\t",
                    ConformanceLevel = ConformanceLevel.Auto
                };
                XmlWriter xmlWriter = XmlWriter.Create(fs, settings);
                serializer.Serialize(xmlWriter, Readings[i], null);
            }
        File.AppendAllLines(resFileName, new string[] { "</" + nameof(Readings) + ">" });

        using (StreamWriter fstream = new StreamWriter($"epoch{Epoch}.txt", false)) {
            fstream.WriteLine($"Time = {QualifyReadings.Last().Sum(p => p.SumTime)}\n" +
                $"Targets collected = {QualifyReadings.Last().Length}\n" +
                $"Quality = {SystemQuality.Last()}\n" +
                $"WayTime = {QualifyReadings.Last().Sum(p => p.WayTime)}\n" +
                $"WorkingTime = {QualifyReadings.Last().Sum(p => p.WorkingTime)}\n" +
                $"SumLevel = {QualifyReadings.Last().Sum(p => p.TakedLevel)}\n" +
                $"LeavedLevel = {QualifyReadings.Last().Sum(p => (p.TakedTarget as Snowdrift).Level)}\n");
        }
    }

    public void SaveStrategy(string resFileName) {
        resFileName.Replace(".xml", "-strategy.xml");
        XmlSerializer serializer = new XmlSerializer(typeof(StrategicSituationReading));
        if (!File.Exists(resFileName))
            using (FileStream fs = new FileStream(resFileName, FileMode.Create)) {
                XmlWriterSettings settings = new XmlWriterSettings() {
                    Indent = true,
                    ConformanceLevel = ConformanceLevel.Auto,
                    WriteEndDocumentOnClose = false
                };
                var writer = XmlWriter.Create(fs, settings);
                writer.WriteStartDocument();
                writer.WriteStartElement(nameof(StrategicReadings));
                writer.Close();
            }
        for (int i = 0; i < StrategicReadings.Length; i++)
            using (FileStream fs = new FileStream(resFileName, FileMode.Append)) {
                XmlWriterSettings settings = new XmlWriterSettings {
                    OmitXmlDeclaration = true,
                    Indent = true,
                    CloseOutput = true,
                    IndentChars = "\t",
                    ConformanceLevel = ConformanceLevel.Auto
                };
                XmlWriter xmlWriter = XmlWriter.Create(fs, settings);
                serializer.Serialize(xmlWriter, StrategicReadings[i], null);
            }
        File.AppendAllLines(resFileName, new string[] { "</" + nameof(StrategicReadings) + ">" });
    }

    public void Dispose() {
        if (Readings.Any()) {
            Backup();
        }
    }
}
