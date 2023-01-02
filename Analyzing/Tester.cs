using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Serialization;

namespace TacticalAgro {
    public class Tester : IDisposable {
        public Model[] Models { get; set; }
        string currentFilePath = "";
        private Director director;
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
        const int attemptsMax = 50;
        public int AttemptsN { get; private set; } = attemptsMax;

        public Tester() {
            Models = new Model[] {
                new Model("T1-10-2", "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside1-10-2.xml",
                    (1, 1, 1), (5F, 0, 0)),
                new Model("T1-10", "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside1-10.xml",
                    (1, 15, 1), (5F, 0, 0)),
                new Model("T1-20", "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside1-20.xml",
                    (1, 25, 1), (5F, 0, 0)),
                new Model("T2-10", "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside2-10.xml",
                    (1, 15, 1), (5F, 0, 0)),
                new Model("T2-20", "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside2-20.xml",
                    (1, 25, 1), (5F, 0, 0)),
                new Model("T4-20", "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside4-20.xml",
                    (1, 25, 1), (5F, 0, 0)),
                new Model("T4-40", "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside4-40.xml",
                    (1, 42, 1), (5F, 0, 0)),
                new Model("S1-10", "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside1-10.xml",
                    (5, 0, 0), (2F, 18F, 0.5F)),
                new Model("S1-20", "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside1-20.xml",
                    (5, 0, 0), (2F, 18F, 0.5F)),
                new Model("S2-10", "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside2-10.xml",
                    (5, 0, 0), (2F, 18F, 0.5F)),
                new Model("S2-20", "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside2-20.xml",
                    (5, 0, 0), (2F, 18F , 0.5F)),
                new Model("S4-20", "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside4-20.xml",
                    (5, 0, 0), (2F, 18F , 0.5F)),
                new Model("S4-40", "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside4-40.xml",
                    (5, 0, 0), (2F, 18F , 0.5F))
            };
            currentFilePath = Models[0].Path;
        }

        public bool NextAttempt() {
            if (--AttemptsN < 1) {
                Models[0].TransportersT = Models[0].TransportersT.SkipLast(1).ToList();
                Models[0].ScalesT = Models[0].ScalesT.SkipLast(1).ToList();
                AttemptsN = attemptsMax;

                if (Models[0].TransportersT.Any())
                    return true;

                SaveResults(Readings);
                readings.Clear();
                return NextModel();
            }
            return true;
        }
        public bool NextModel() {
            Models = Models.Skip(1).ToArray();
            if (Models.Any())
                currentFilePath = Models[0].Path;
            else return false;

            return true;
        }
        public Director LoadModel(string path) {
            XmlSerializer serializer = new XmlSerializer(typeof(Director));
            using (FileStream fs = new FileStream(path, FileMode.Open)) {
                director = serializer.Deserialize(fs) as Director;
                if (director == null) return null;

                var @base = director.Map.Bases[0];
                Transporter[] transporters = new Transporter[Models[0].TransportersT[^1]];
                for (int i = 0; i < Models[0].TransportersT[^1]; i++) {
                    transporters[i] = new Transporter(@base.Position);
                    director.Add(transporters[i]);
                }
                 director.Scale = Models[0].ScalesT[^1];

                if (currentFilePath != path)
                    currentFilePath = path;
                fs.Close();
            }
            return director;
        }
        public void SaveResults(Director director, TimeSpan wayTime, TimeSpan fullTime, ref double iterations) {
            var analyzer = new Reading() {
                ModelName = Models[0].Name,
                TransportersCount = director.Transporters.Length,
                Scale = Math.Round(director.Scale, 3),
                CalcTime = director.ThinkingTime.TotalSeconds,
                WayTime = wayTime.TotalSeconds,
                FullTime = fullTime.TotalSeconds,
                TraversedWay = director.TraversedWaySum,
                STransporterWay = new double[director.Transporters.Length],
                TargetsCount = director.Targets.Length,
                Iterations = Math.Round(iterations)
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
        private void SaveResults(Reading[] _readings) {
            string resFileName = $"Results_{_readings[0].ModelName}.xml";
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
            for (int i = 0; i < _readings.Length; i++)
                using (FileStream fs = new FileStream(resFileName, FileMode.Append)) {
                    XmlWriterSettings settings = new XmlWriterSettings {
                        OmitXmlDeclaration = true,
                        Indent = true,
                        CloseOutput = true,
                        IndentChars = "\t",
                        ConformanceLevel = ConformanceLevel.Auto
                    };
                    XmlWriter xmlWriter = XmlWriter.Create(fs, settings);
                    serializer.Serialize(xmlWriter, _readings[i], null);
                }
                File.AppendAllLines(resFileName, new string[]{ "</" + nameof(Readings) + ">" });
        }

        public void Dispose() {
            if (Readings.Any()) {
                Readings[0].ModelName += "-autosave-" + DateTime.Now.ToShortDateString();
                SaveResults(Readings);
            }
        }
    }
}
