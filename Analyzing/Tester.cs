using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
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
                /*new Model("T1-10-2", "Inside1.xml", 10, (1, 1, 1), (5F, 0, 0)),
                new Model("T1-10", "Inside1.xml", 10, (1, 15, 1), (5F, 0, 0)),
                new Model("T1-20", "Inside1.xml", 20, (1, 25, 1), (5F, 0, 0)),
                new Model("T2-10", "Inside2.xml", 10, (1, 15, 1), (5F, 0, 0)),
                new Model("T2-20", "Inside2.xml", 20, (1, 25, 1), (5F, 0, 0)),
                new Model("T4-20", "Inside4.xml", 20, (1, 25, 1), (5F, 0, 0)),
                new Model("T4-40", "Inside4.xml", 40, (1, 42, 1), (5F, 0, 0)),
                new Model("S1-10", "Inside1.xml", 10, (5, 0, 0), (2F, 18F, 0.5F)),
                new Model("S1-20", "Inside1.xml", 20, (5, 0, 0), (2F, 18F, 0.5F)),
                new Model("S2-10", "Inside2.xml", 10, (5, 0, 0), (2F, 18F, 0.5F)),
                new Model("S2-20", "Inside2.xml", 20, (5, 0, 0), (2F, 18F , 0.5F)),
                new Model("S4-20", "Inside4.xml", 20, (5, 0, 0), (2F, 18F , 0.5F)),
                new Model("S4-40", "Inside4.xml", 40, (5, 0, 0), (2F, 18F , 0.5F))
                new Model("Standart10.xml"),
                new Model("Standart20.xml"),
                new Model("StandartCenter10.xml"),
                new Model("StandartCenter20.xml"),
                new Model("Quad10", "Quad3.xml", 12, (1, 25, 1), (5F, 0, 0)),
                new Model("Quad20", "Quad3.xml", 24, (1, 25, 1), (5F, 0, 0)),
                new Model("Lines10", "Lines3.xml", 12, (1, 25, 1), (5F, 0, 0)),
                new Model("Lines20", "Lines3.xml", 24, (1, 25, 1), (5F, 0, 0)),*/
            };
            foreach (string fileName in Directory.GetFiles(Path.Combine(Paths.Default.Tests, "Active")))
                models.Add(new Model(fileName));
            Models = models.ToArray();
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
