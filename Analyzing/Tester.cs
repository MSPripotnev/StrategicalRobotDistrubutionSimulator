using System.IO;
using System.Xml.Serialization;

namespace SRDS.Analyzing;
using SRDS.Analyzing.Models;
using SRDS.Direct;
using SRDS.Direct.Tactical;

public class Tester {
    public IModel[] Models { get; set; } = Array.Empty<IModel>();
    string currentFilePath = "";
    public Director? ActiveDirector { get; set; }
    public int AttemptsN { get; set; } = 0;
    public event EventHandler? AttemptStarted;
    public event EventHandler? AttemptCompleted;
    public event EventHandler? AttemptFailed;
    public event EventHandler? ModelSwitched;
    public Tester() {
        if (Directory.Exists(Path.Combine(Paths.Default.Tests, "Complete"))) {
            foreach (string fileName in Directory.GetFiles(Path.Combine(Paths.Default.Tests, "Complete")))
                Directory.Move(fileName, fileName.Replace("Complete", "Active"));
            LoadModels();
        }
    }

    public void LoadModels() {
        List<IModel> models = new List<IModel>();
        foreach (string fileName in Directory.GetFiles(Path.Combine(Paths.Default.Tests, "Active"))) {
            if (fileName.EndsWith(".prm"))
                models.Add(new ParametrRangeGeneratedModel(fileName));
            else if (fileName.EndsWith(".cmt"))
                models.Add(new CopyModel(fileName));
        }
        Models = models.ToArray();
        if (Models.Any() && Models[0].Path is string path) {
            currentFilePath = path;
            AttemptsN = Models[0].MaxAttempts;
            LoadModel(currentFilePath);
        }
    }
    public void NextAttempt() {
        AttemptCompleted?.Invoke(this, new());
        if (--AttemptsN < 1) {
            if (Models[0] is ParametrRangeGeneratedModel pm) {
                pm.TransportersT = pm.TransportersT.SkipLast(1).ToList();
                pm.ScalesT = pm.ScalesT.SkipLast(1).ToList();

                if (!pm.TransportersT.Any()) {
                    NextModel();
                }
            } else if (Models[0] is CopyModel) {
                NextModel();
            }
            if (Models.Any()) {
                AttemptsN = Models[0].MaxAttempts;
            }
        }
        if (Models.Any())
            AttemptStarted?.Invoke(this, new());
    }
    public void NextModel() {
        if (!Models.Any() || Models[0].Path is not string path) return;
        Directory.Move(path, Path.Combine(Paths.Default.Tests, "Complete",
            path[(Array.LastIndexOf(path.ToCharArray(), '\\') + 1)..path.Length]));
        Models = Models.Skip(1).ToArray();
        if (Models.Any())
            ModelSwitched?.Invoke(Models[0], new());
        else ModelSwitched?.Invoke(null, new());
        return;
    }
    public void StopAttempt() {
        AttemptFailed?.Invoke(this, new());
    }
    public Director ReloadModel() {
        Recorder r = new Recorder();
        Learning l = new Learning();
        Type? q = null;
        if (ActiveDirector != null) {
            r = ActiveDirector.Recorder;
            l = ActiveDirector.Learning;
            q = ActiveDirector.Distributor.Qualifier.GetType();
        }
        ActiveDirector = Models[0].Unpack();
        ActiveDirector.Recorder = r;
        ActiveDirector.Learning = l;
        ActiveDirector.Distributor = new(q, ActiveDirector.Map, TaskDistributor.GetSnowdriftControlFuzzyQualifyVariables());
        return ActiveDirector;
    }
    public void LoadModel(string path) {
        XmlSerializer serializer;
        if (path.EndsWith(".pgrm"))
            serializer = new XmlSerializer(typeof(ParametrRangeGeneratedModel));
        else if (path.EndsWith(".cm"))
            serializer = new XmlSerializer(typeof(CopyModel));
        else return;

        using FileStream fs = new FileStream(path, FileMode.Open);
        if (serializer.Deserialize(fs) is not IModel model)
            return;
        ActiveDirector = model.Unpack();

        if (currentFilePath != path)
            currentFilePath = path;
        fs.Close();
    }
}
