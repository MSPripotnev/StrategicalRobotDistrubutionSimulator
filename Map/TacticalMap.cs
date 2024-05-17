using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;

namespace TacticalAgro.Map {
    public class TacticalMap : INotifyPropertyChanged {
        private Obstacle[] obstacles;
        private Base[] bases;
        private Size borders;
        private string path;

        public event PropertyChangedEventHandler? PropertyChanged;

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
        [XmlIgnore]
        public string Path { get; set; }
        [XmlArray("Obstacles")]
        [XmlArrayItem("Obstacle")]
        public Obstacle[] Obstacles {
            get => obstacles;
            set {
                obstacles = value;
                PropertyChanged?.Invoke(Obstacles, new PropertyChangedEventArgs(nameof(Obstacles)));
            }
        }
        [XmlArray("Bases")]
        [XmlArrayItem("Base")]
        public Base[] Bases {
            get => bases;
            set {
                bases = value;
                PropertyChanged?.Invoke(Bases, new PropertyChangedEventArgs(nameof(Bases)));
            }
        }
        public Size Borders {
            get => borders;
            set {
                borders = value;
                PropertyChanged?.Invoke(Borders, new PropertyChangedEventArgs(nameof(Borders)));
            }
        }
        public void Save() => Save(path);
        public void Save(string path) {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(TacticalMap));
			XmlWriterSettings settings = new XmlWriterSettings() {
				Indent = true,
				IndentChars = "\t",
			};
			using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate)) {
                xmlSerializer.Serialize(fs, this);
                fs.Close();
            }
            Path = path;
            Name = path[path.LastIndexOf('\\')..^4];
        }

        public TacticalMap() {
            Obstacles = Array.Empty<Obstacle>();
            Bases = Array.Empty<Base>();
            Borders = new Size(0, 0);
        }
        public TacticalMap(Obstacle[] _obstacles, Base[] _bases, Size _borders) {
            Obstacles = _obstacles;
            Bases = _bases;
            Borders = _borders;
        }

        public TacticalMap(string path) {
            if (File.Exists(path)) {
                TacticalMap newMap;
                using (FileStream fs = new FileStream(path, FileMode.Open)) {
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(TacticalMap));
                    newMap = (TacticalMap)xmlSerializer.Deserialize(fs);
                    fs.Close();
                }
                Obstacles = newMap.Obstacles;
                Bases = newMap.Bases;
                Borders = newMap.Borders;
                Name = newMap.Name;
            } else
                throw new FileNotFoundException();
            Path = path;
        }
    }
}
