using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;
using System.Runtime.CompilerServices;

namespace TacticalAgro.Analyzing {
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
        public void SaveResults(Director director, string modelName, TimeSpan wayTime, TimeSpan fullTime, ref double iterations) {
            var analyzer = new Reading() {
                ModelName = modelName,
                TransportersCount = director.Transporters.Length,
                Scale = Math.Round(director.Scale, 3),
                CalcTime = director.ThinkingTime.TotalSeconds,
                ThinkingIterations = director.ThinkingIterations,
                WayTime = director.TraversedWaySum / (0.050 * director.Transporters[0].Speed),
                WayIterations = director.WayIterations,
                FullTime = fullTime.TotalSeconds,
                DistributeIterations = Math.Round(iterations),
                TraversedWay = director.TraversedWaySum,
                STransporterWay = new double[director.Transporters.Length],
                TargetsCount = director.Targets.Length,
            };
            analyzer.DistributeTime = analyzer.FullTime - analyzer.WayTime - analyzer.CalcTime;
            if (director.Transporters.Any()) {
                analyzer.TransportersSpeed = Math.Round(director.Transporters[0].Speed, 8);
                for (int i = 0; i < director.Transporters.Length; i++)
                    analyzer.STransporterWay[i] = director.Transporters[i].TraversedWay;
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
