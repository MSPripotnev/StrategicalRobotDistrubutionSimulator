using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace SRDS.Analyzing {
    using SRDS.Direct;
    internal class Recorder : IDisposable {

        List<Reading> readings = new List<Reading>();
        [XmlArray(ElementName = "Readings")]
        [XmlArrayItem(ElementName = "Reading")]
        public Reading[] Readings {
            get {
                return readings.ToArray();
            }
            set {
                readings = new List<Reading>(value);
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
            var analyzer = new Reading() {
                ModelName = modelName,
                TransportersCount = director.Agents.Length,
                Scale = Math.Round(director.Scale, 3),
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
            iterations = 0;
        }
        private void SaveInXMLFile(string resFileName) {
            XmlSerializer serializer = new XmlSerializer(typeof(Reading));
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
        }

        public void Dispose() {
            if (Readings.Any()) {
                Backup();
            }
        }
    }
}
