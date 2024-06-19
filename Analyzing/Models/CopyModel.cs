using SRDS.Direct;
using SRDS.Agents;
using SRDS.Map.Targets;
using SRDS.Agents.Drones;
using System.IO;
using System.Windows;
using System.Xml.Serialization;

namespace SRDS.Analyzing.Models;
public class CopyModel : IModel {
    private Director model;
    [XmlIgnore]
    public string Path { get; set; }
    private string modelPath;
    public string ModelPath {
        get => modelPath;
        set {
            model = Director.Deserialize(value);
            modelPath = value;
        }
    }
    public string Name { get; set; }
    public int MaxAttempts { get; set; } = 250;
    private int seed;
    public int Seed { 
        get => seed; 
        set => seed = model.Seed = value;
    }
    public CopyModel() {
        model = new Director();
    }
    public CopyModel(Director director) : this() {
        model.Agents = director.Agents.Clone() as Agent[];
        model.Targets = director.Targets.Clone() as Target[];
        for (int i = 0; i < director.Agents.Length; i++) {
            if (director.Agents[i] is SnowRemover a)
                model.Agents[i] = new SnowRemover(a.Position, a.Devices);
            else if (director.Agents[i] is Transporter t)
                model.Agents[i] = new Transporter(t.Position);
            model.Agents[i].Home = director.Agents[i].Home;
        }
        model.MapPath = director.MapPath;
        model.Scale = director.Scale;
        model.Time = director.Time;
    }
    public CopyModel(string path) {
        path = System.IO.Path.Combine(Paths.Default.Tests, path);
        if (File.Exists(path))
            using (FileStream fs = new FileStream(path, FileMode.Open)) {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(CopyModel));
                CopyModel m = (CopyModel)xmlSerializer.Deserialize(fs);
                Name = m.Name is not null && m.Name != "" ? m.Name : System.IO.Path.GetFileNameWithoutExtension(path);
                
                ModelPath = m.ModelPath;
                Seed = m.Seed;
                MaxAttempts = m.MaxAttempts;

                Path = path;
            }
        else MessageBox.Show("Failed to load model: " + path);
    }
    public Director Unpack() {
        var res = new Director() {
            MapPath = model.MapPath,
            Scale = model.Scale,
            Time = model.Time,
            Seed = model.Seed,
        };
        res.Agents = model.Agents.Clone() as Agent[];
        for (int i = 0; i < model.Agents.Length; i++) {
            if (model.Agents[i] is SnowRemover a)
                res.Agents[i] = new SnowRemover(a.Position, a.Devices);
            else if (model.Agents[i] is Transporter t)
                res.Agents[i] = new Transporter(t.Position);
            res.Agents[i].Home = model.Agents[i].Home;
            res.Agents = res.Agents;
        }
        res.Targets = model.Targets.Clone() as Target[];
        return res;
    }
}
