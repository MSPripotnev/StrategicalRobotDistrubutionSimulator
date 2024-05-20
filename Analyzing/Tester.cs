using System.IO;
using System.Xml.Serialization;

namespace TacticalAgro.Analyzing {
    public class Tester {
        public Model[] Models { get; set; }
        string currentFilePath = "";
        private Director director;
        public int AttemptsN { get; private set; } = Testing.Default.AttemptsMax;
        public event EventHandler AttemptStarted;
        public event EventHandler AttemptCompleted;
        public event EventHandler AttemptFailed;
        public event EventHandler ModelSwitched;
        public Tester() {
            List<Model> models = new List<Model> {
            };
            if (Directory.Exists(Path.Combine(Paths.Default.Tests, "Complete"))) {
                foreach (string fileName in Directory.GetFiles(Path.Combine(Paths.Default.Tests, "Complete")))
                    Directory.Move(fileName, fileName.Replace("Complete", "Active"));
                LoadModels();
            }
        }

        public void LoadModels() {
            List<Model> models = new List<Model>();
            foreach (string fileName in Directory.GetFiles(Path.Combine(Paths.Default.Tests, "Active")))
                models.Add(new Model(fileName));
            Models = models.ToArray();
            if (Models.Any())
                currentFilePath = Models[0].Path;
        }

        public void NextAttempt() {
            AttemptCompleted(this, new());
            if (--AttemptsN < 1) {
                Models[0].TransportersT = Models[0].TransportersT.SkipLast(1).ToList();
                Models[0].ScalesT = Models[0].ScalesT.SkipLast(1).ToList();
                AttemptsN = Testing.Default.AttemptsMax;

                if (!Models[0].TransportersT.Any()) {
                    NextModel();
                }
            }
            if (Models.Any())
                AttemptStarted(this, new());
        }
        public void NextModel() {
            Directory.Move(Models[0].Path,
                Path.Combine(Paths.Default.Tests, "Complete",
                Models[0].Path[(Array.LastIndexOf(Models[0].Path.ToCharArray(), '\\')+1)..Models[0].Path.Length]));
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
            director = new Director(Models[0]);
            return director;
        }
        public Director LoadModel(string path) {
            XmlSerializer serializer = new XmlSerializer(typeof(Model));
            using (FileStream fs = new FileStream(path, FileMode.Open)) {
                Model model = serializer.Deserialize(fs) as Model;
                if (model == null) return null;
                director = new Director(model);

                if (currentFilePath != path)
                    currentFilePath = path;
                fs.Close();
            }
            return director;
        }
    }
}
