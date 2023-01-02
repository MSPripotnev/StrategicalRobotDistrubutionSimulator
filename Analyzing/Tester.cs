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
    public class Tester {
        public string[] ModelsFiles { get; set; }
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
        private int XRange = 20;
        private float scaleMax = 5F;
        const int attemptsMax = 10;
        public int AttemptsN { get; private set; } = attemptsMax;
        private List<int> TransportersT = new List<int>();
        private List<float> ScalesT = new List<float>();

        public Tester() {
            ModelsFiles = new string[] {
                "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside1-10.xml",
                "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside1-20.xml",
                "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside2-10.xml",
                "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside2-20.xml"
            };
            currentFilePath = ModelsFiles[0];
            LoadTest();
        }

        private void LoadTest() {
            TransportersT = new List<int>(XRange);
            ScalesT = new List<float>(XRange);
            for (int i = 0; i < XRange; i++) {
                TransportersT.Add(i + 1);
                ScalesT.Add(scaleMax);
            }
        }

        public bool NextAttempt() {
            if (--AttemptsN < 1) {
                TransportersT = TransportersT.SkipLast(1).ToList();
                ScalesT = ScalesT.SkipLast(1).ToList();
                AttemptsN = attemptsMax;

                if (TransportersT.Any())
                    return true;

                SaveResults(Readings);
                readings.Clear();
                return NextModel();
            }
            return true;
        }
        public bool NextModel() {
            ModelsFiles = ModelsFiles.Skip(1).ToArray();
            if (ModelsFiles.Any())
                currentFilePath = ModelsFiles[0];
            else return false;

            LoadTest();
            return true;
        }
        public Director LoadModel(string path) {
            XmlSerializer serializer = new XmlSerializer(typeof(Director));
            using (FileStream fs = new FileStream(path, FileMode.Open)) {
                director = serializer.Deserialize(fs) as Director;
                if (director == null) return null;

                var @base = director.Map.Bases[0];
                Transporter[] transporters = new Transporter[TransportersT[^1]];
                for (int i = 0; i < TransportersT[^1]; i++) {
                    transporters[i] = new Transporter(@base.Position);
                    director.Add(transporters[i]);
                }
                director.Scale = ScalesT[^1];

                if (currentFilePath != path)
                    currentFilePath = path;
                fs.Close();
            }
            return director;
        }
        public void SaveResults(Director director, TimeSpan timerInterval, double fullTime, ref double iterations) {
            var analyzer = new Reading() {
                Scale = director.Scale,
                ModelName = currentFilePath.Substring(
                    currentFilePath.LastIndexOf('\\') + 1),
                TransportersCount = director.Transporters.Length,
                CalcTime = Math.Round(director.ThinkingTime.TotalMilliseconds),
                WayTime = (iterations*timerInterval).TotalSeconds,//Math.Round((DateTime.Now - startTime + tempTime - director.ThinkingTime).TotalSeconds, 3),
                FullTime = fullTime,
                TraversedWay = Math.Round(director.TraversedWaySum),
                STransporterWay = new double[director.Transporters.Length],
                TargetsCount = director.Targets.Length,
                Iterations = Math.Round(iterations)
            };
            analyzer.RandomTime = analyzer.FullTime - analyzer.WayTime - analyzer.CalcTime / 1000;
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
    }
}
