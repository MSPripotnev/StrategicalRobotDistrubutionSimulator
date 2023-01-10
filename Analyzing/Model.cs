using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Threading.Tasks;

namespace TacticalAgro {
    public class ParametrRange {
        public bool IsConst { get; init; }
        private List<double> Values;

        public ParametrRange((double start, double end, double step) range) {
            if (IsConst = (range.step == 0)) {
                Values = Enumerable.Repeat(Math.Round(range.start, 15), (int)range.end).ToList();
            } else {
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
    public class Model {
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlIgnore]
        public string Path { get; set; }
        [XmlArray("Transporters")]
        [XmlArrayItem("TransporterCount")]
        public List<int> TransportersT { get; set; } = new List<int>();
        [XmlArray("Scales")]
        [XmlArrayItem("Scale")]
        public List<float> ScalesT { get; set; } = new List<float>();
        public Model() {
            TransportersT = new List<int>();
            ScalesT = new List<float>();
        }
        public Model(string name, (int, int, int) transporterRange, (float, float, float) scaleRange) {
            Name = name;
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
            using (FileStream fs = new FileStream($"{Name}.xml", FileMode.Create)) {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(Model));
                XmlWriterSettings settings = new XmlWriterSettings() {
                    Indent = true,
                    CloseOutput = true,
                };
                xmlSerializer.Serialize(fs, this);
            }
        }
        public Model(string path) {
            if (System.IO.File.Exists(path))
                using (FileStream fs = new FileStream(path, FileMode.Open)) {
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(Model));
                    xmlSerializer.Deserialize(fs);
                }
        }
    }
}
