using SRDS.Direct;
using System.IO;
using System.Windows;
using System.Xml.Serialization;
using SRDS.Direct.Agents.Drones;
using SRDS.Direct.Agents;
using SRDS.Model.Targets;
using SRDS.Model.Map.Stations;

namespace SRDS.Analyzing.Models;
public class CopyModel : IModel {
    private Director model;
    [XmlIgnore]
    public string Path { get; set; }
    private string modelPath;
    public string ModelPath {
        get => modelPath;
        set {
            model = Director.Deserialize(value) ?? throw new FileFormatException(value);
            modelPath = value;
        }
    }
    public string Name { get; set; }
    public int MaxAttempts { get; set; } = 250;
    public DateTime? AttemptTime { get; set; }
    private int seed;
    public int Seed {
        get => seed;
        set => seed = model.Seed = value;
    }
    public CopyModel() {
        model = new Director();
        Name = "Default copy model";
        modelPath = Path = model.MapPath.Replace(".xml", $"-{nameof(CopyModel)}.xml");
    }
    public CopyModel(Director director) {
        model = new Director(director.Map.Borders);
        model.MapPath = director.MapPath;
        model.Agents = director.Agents.Clone() as Agent[] ?? throw new Exception();
        model.Targets = director.Targets.Clone() as Target[] ?? throw new Exception();
        for (int i = 0; i < director.Agents.Length; i++) {
            if (director.Agents[i] is SnowRemover a)
                model.Agents[i] = new SnowRemover(a.Position, a.Devices);
            else if (director.Agents[i] is Transporter t)
                model.Agents[i] = new Transporter(t.Position);
            model.Agents[i].Home = model.Map.Stations.OfType<AgentStation>().MinBy(p => (p.Position - (director.Agents[i].Home?.Position ?? model.Agents[i].Position)).Length);
        }
        model.PathScale = director.PathScale;
        model.Time = director.Time;
        modelPath = Path = model.MapPath.Replace(".xml", $"-{nameof(CopyModel)}.xml");
        Name = $"Copy model from {model.MapPath}";
    }
    public CopyModel(string path) {
        path = System.IO.Path.Combine(Paths.Default.Tests, path);
        if (!File.Exists(path))
            throw new FileFormatException("Failed to load model: " + path);

        using FileStream fs = new FileStream(path, FileMode.Open);
        XmlSerializer xmlSerializer = new XmlSerializer(typeof(CopyModel));
        CopyModel m = (CopyModel?)xmlSerializer.Deserialize(fs) ?? throw new FileFormatException();
        Name = m.Name is not null && m.Name != "" ? m.Name : System.IO.Path.GetFileNameWithoutExtension(path);

        modelPath = ModelPath = m.ModelPath;
        Seed = m.Seed;
        MaxAttempts = m.MaxAttempts;

        Path = path;
        model = m.Unpack();
    }
    public void Save(string path) {
        using FileStream fs = new FileStream(path, FileMode.Create);
        XmlSerializer xmlSerializer = new XmlSerializer(typeof(CopyModel));
        xmlSerializer.Serialize(fs, this);
    }
    public Director Unpack() {
        Director res = new Director() {
            MapPath = model.MapPath,
            PathScale = model.PathScale,
            Time = model.Time,
            Seed = model.Seed,
        };
        res.Agents = model.Agents.Clone() as Agent[] ?? throw new Exception();
        for (int i = 0; i < model.Agents.Length; i++) {
            if (model.Agents[i] is SnowRemover a)
                res.Agents[i] = new SnowRemover(a.Position, a.Devices);
            else if (model.Agents[i] is Transporter t)
                res.Agents[i] = new Transporter(t.Position);
            res.Agents = res.Agents;
            if (model.Agents[i].Home is not null)
                res.Agents[i].Home = res.Map.Stations.OfType<AgentStation>().MinBy(p => (p.Position - (model.Agents[i].Home?.Position ?? model.Agents[i].Position)).Length);
        }
        res.Targets = model.Targets.Clone() as Target[] ?? throw new Exception();
        return res;
    }
}
