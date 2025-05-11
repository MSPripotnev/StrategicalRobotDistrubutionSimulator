using System.Data;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace SRDS.Analyzing;

using Direct;
using Direct.Executive;
using Direct.Strategical;
using Direct.Tactical;
using Model.Targets;

public class Recorder : IDisposable {
    public int Epoch { get => SystemQualifyReadings.Count; }
    public List<double> SystemQualifyReadings { get; init; } = new();
    List<List<StrategyTaskQualifyReading>> allEpochsTaskQualifyReadings = new();
    [XmlArray(ElementName = "AllEpochsTaskQualifyReadings")]
    [XmlArrayItem(ElementName = "AllEpochsTaskQualifyReadings")]
    public StrategyTaskQualifyReading[][] AllEpochsTaskQualifyReadings {
        get => allEpochsTaskQualifyReadings.Select(p => p.ToArray()).ToArray();
        set {
            allEpochsTaskQualifyReadings = new List<List<StrategyTaskQualifyReading>>(value.Select(p => p.ToList()));
        }
    }
    [XmlIgnore]
    public StrategyTaskQualifyReading[] CurrentEpochSystemQualifyReadings {
        get => AllEpochsTaskQualifyReadings.LastOrDefault(Array.Empty<StrategyTaskQualifyReading>());
    }
    List<StrategicSituationReading> systemEpochTimeReadings = new List<StrategicSituationReading>();
    [XmlArray(ElementName = "SystemEpochTimeReadings")]
    [XmlArrayItem(ElementName = "SystemEpochTimeReadings")]
    public StrategicSituationReading[] SystemEpochTimeReadings {
        get => systemEpochTimeReadings.ToArray();
        set {
            systemEpochTimeReadings = new List<StrategicSituationReading>(value);
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
        var vs = AllEpochsTaskQualifyReadings.ToList();
        vs.Add(director.Scheduler.TaskQualifies.Values.ToArray());

        // Take last epoch and aggregated value of actions quality
        double qEnv = 1, qL = 5, qF = 2, fuelCostRub = 70, deicingCostRub = 80;
        var last = SystemEpochTimeReadings.Last();
        SystemQualifyReadings.Add(qEnv * SystemEpochTimeReadings.Sum(p => p.RemovedSnow) / SystemEpochTimeReadings.Length
            + qL * SystemEpochTimeReadings.Sum(p => p.CurrentIcy) / SystemEpochTimeReadings.Length
            + qF * (fuelCostRub * SystemEpochTimeReadings.Sum(p => p.FuelConsumption)
                + deicingCostRub * SystemEpochTimeReadings.Sum(p => p.DeicingConsumption) / SystemEpochTimeReadings.Length));

        AllEpochsTaskQualifyReadings = vs.ToArray();
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
        /*
        using (StreamWriter fstream = new StreamWriter($"epoch{Epoch}.txt", false)) {
            fstream.WriteLine($"Time = {AllEpochsTaskQualifyReadings.Last().Sum(p => p.SumTime)}\n" +
                $"Targets collected = {AllEpochsTaskQualifyReadings.Last().Length}\n" +
                $"Quality = {SystemQualifyReadings.Last()}\n" +
                $"TaskTime = {AllEpochsTaskQualifyReadings.Last().Sum(p => p.TaskTime)}\n" +
                $"WorkingTime = {AllEpochsTaskQualifyReadings.Last().Sum(p => p.WorkingTime)}\n" +
                $"SumLevel = {AllEpochsTaskQualifyReadings.Last().Sum(p => p.TakedLevel)}\n" +
                $"LeavedLevel = {AllEpochsTaskQualifyReadings.Last().Sum(p => (p.TakedTarget as Snowdrift).Level)}\n");
        }
        */
    }

    public void SaveStrategy(string resFileName) {
        resFileName = resFileName.Replace(".xml", "-strategy.xml");
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
                writer.WriteStartElement(nameof(SystemEpochTimeReadings));
                writer.Close();
            }
        for (int i = 0; i < SystemEpochTimeReadings.Length; i++)
            using (FileStream fs = new FileStream(resFileName, FileMode.Append)) {
                XmlWriterSettings settings = new XmlWriterSettings {
                    OmitXmlDeclaration = true,
                    Indent = true,
                    CloseOutput = true,
                    IndentChars = "\t",
                    ConformanceLevel = ConformanceLevel.Auto
                };
                XmlWriter xmlWriter = XmlWriter.Create(fs, settings);
                serializer.Serialize(xmlWriter, SystemEpochTimeReadings[i], null);
            }
        File.AppendAllLines(resFileName, new string[] { "</" + nameof(SystemEpochTimeReadings) + ">" });
    }

    public void Dispose() {
        if (Readings.Any()) {
            Backup();
        }
    }
}
