using System.IO;
using System.Xml.Serialization;

namespace SRDS.Analyzing;
using SRDS.Analyzing.Models;
using SRDS.Direct;
using SRDS.Direct.Strategical.Qualifiers;

public class Tester {
    public IModel[] Models { get; set; } = Array.Empty<IModel>();
    string currentFilePath = "";
    public Director ActiveDirector { get; set; }
    public int AttemptsN { get; set; } = 0;
    public event EventHandler AttemptStarted;
    public event EventHandler AttemptCompleted;
    public event EventHandler AttemptFailed;
    public event EventHandler ModelSwitched;
    public Tester() {
        List<ParametrRangeGeneratedModel> models = new List<ParametrRangeGeneratedModel> {
        };
        if (Directory.Exists(Path.Combine(Paths.Default.Tests, "Complete"))) {
            foreach (string fileName in Directory.GetFiles(Path.Combine(Paths.Default.Tests, "Complete")))
                Directory.Move(fileName, fileName.Replace("Complete", "Active"));
            LoadModels();
        }
    }

    public void LoadModels() {
        List<IModel> models = new List<IModel>();
        foreach (string fileName in Directory.GetFiles(Path.Combine(Paths.Default.Tests, "Active"))) {
            if (fileName.EndsWith(".prgm"))
                models.Add(new ParametrRangeGeneratedModel(fileName));
            else if (fileName.EndsWith(".cm"))
                models.Add(new CopyModel(fileName));
        }
        Models = models.ToArray();
        if (Models.Any()) {
            currentFilePath = Models[0].Path;
            AttemptsN = Models[0].MaxAttempts;
            LoadModel(currentFilePath);
        }
    }
    public void NextAttempt() {
        AttemptCompleted(this, new());
        if (--AttemptsN < 1) {
            if (Models[0] is ParametrRangeGeneratedModel pm) {
                pm.TransportersT = pm.TransportersT.SkipLast(1).ToList();
                pm.ScalesT = pm.ScalesT.SkipLast(1).ToList();

                if (!pm.TransportersT.Any()) {
                    NextModel();
                }
            } else if (Models[0] is CopyModel cm) {
                NextModel();
            }
            if (Models.Any()) {
                AttemptsN = Models[0].MaxAttempts;
            }
        }
        if (Models.Any())
            AttemptStarted(this, new());
    }
    public void NextModel() {
        Directory.Move(Models[0].Path,
            Path.Combine(Paths.Default.Tests, "Complete",
            Models[0].Path[(Array.LastIndexOf(Models[0].Path.ToCharArray(), '\\') + 1)..Models[0].Path.Length]));
        Models = Models.Skip(1).ToArray();
        if (Models.Any())
            ModelSwitched(Models[0], new());
        else ModelSwitched(null, new());
        return;
    }
    public void StopAttempt() {
        AttemptFailed(this, new());
    }
    public Director ReloadModel() {
        Recorder r = new Recorder();
        Learning l = new Learning();
        IQualifier q = new FuzzyQualifier();
        if (ActiveDirector != null) {
            r = ActiveDirector.Recorder;
            l = ActiveDirector.Learning;
            q = ActiveDirector.Qualifier;
        }
        ActiveDirector = Models[0].Unpack();
        ActiveDirector.Recorder = r;
        ActiveDirector.Learning = l;
        ActiveDirector.Qualifier = q;
        return ActiveDirector;
    }
    public void LoadModel(string path) {
        XmlSerializer serializer;
        if (path.EndsWith(".pgrm"))
            serializer = new XmlSerializer(typeof(ParametrRangeGeneratedModel));
        else if (path.EndsWith(".cm"))
            serializer = new XmlSerializer(typeof(CopyModel));
        else return;

        using (FileStream fs = new FileStream(path, FileMode.Open)) {
            if (serializer.Deserialize(fs) is not IModel model)
                return;
            ActiveDirector = model.Unpack();

            if (currentFilePath != path)
                currentFilePath = path;
            fs.Close();
        }
    }
}
