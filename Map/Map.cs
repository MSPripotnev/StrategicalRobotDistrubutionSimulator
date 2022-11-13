using System.Drawing.Drawing2D;
using System.Threading.Tasks;

namespace TacticalAgro {
    public partial class Map : Form {
        Director director;
        double workTime = 0;
        int iterations = 0;
        Task[] directorTasks = new Task[2];

        public Map() {
            InitializeComponent();
            director = new Director();
            directorTasks[0] = new Task(() => { director.Work(); });
            directorTasks[1] = new Task(() => { director.DistributeTask(); });
        }

        private void refreshTimer_Tick(object sender, EventArgs e) {

            workTime += refreshTimer.Interval;
            iterations++;
            
            
            if (directorTasks[0].IsCompleted) {
                directorTasks[0] = new Task(() => { director.Work(); });
                directorTasks[0].Start();
            }
                
            if (directorTasks[1].IsCompleted) {
                directorTasks[1] = new Task(() => { director.DistributeTask(); });
                directorTasks[1].Start();
            }
                
            if (director.checkMission()) {
                Task.WaitAll(directorTasks);
                director.Work();
                refreshTimer.Stop();
                startB.Text = "Запуск";
                //return;
            }
            Refresh();
            mapPanel.Invalidate();
        }
        public override void Refresh() {
            base.Refresh();

            collectedObjsCountL.Text = director.CollectedTargets.Length.ToString();
            currentObjsCountL.Text = (director.Targets.Length - director.CollectedTargets.Length).ToString();
            timeCountL.Text = Math.Round(workTime / 1000, 6).ToString() + " s";
            iterationsCountL.Text = iterations.ToString();
        }
        const int standartRobotSize = 30;
        const int standartObjectSize = 15;
        private void mapPanel_Paint(object sender, PaintEventArgs e) {
            for (int i = 0; i < director.AllObjectsOnMap.Count; i++) {
                IMoveable obj = director.AllObjectsOnMap[i];
                Pen pen = new Pen(obj.Color, 5);
                if (obj is Transporter)
                    e.Graphics.DrawEllipse(pen,
                        obj.Position.X, obj.Position.Y,
                        standartRobotSize, standartRobotSize);
                else if (obj is Target)
                    e.Graphics.DrawEllipse(pen,
                        obj.Position.X + standartObjectSize / 2, obj.Position.Y + standartObjectSize / 2,
                        standartObjectSize, standartObjectSize);
                else if (obj is Scout) {
                    e.Graphics.DrawEllipse(pen,
                        obj.Position.X, obj.Position.Y,
                        standartRobotSize, standartRobotSize);
                } 
                else if (obj is Obstacle o) {
                    e.Graphics.FillPolygon(new HatchBrush(HatchStyle.LightDownwardDiagonal, obj.Color), o.Borders);
                }
            }
        }

        private void startB_Click(object sender, EventArgs e) {
            if (startB.Text == "Запуск") {
                directorTasks[0].Start();
                directorTasks[1].Start();
                refreshTimer.Start();
                startB.Text = "Стоп";
            } else {
                refreshTimer.Stop();
                startB.Text = "Запуск";
            }
        }
        (int, int) newObjectPos = (0, 0);
        private void mapPanel_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Right) {
                var loc = (mapPanel.PointToScreen(e.Location));
                newObjectPos = (e.X, e.Y);
                mainCMS.Show(loc.X, loc.Y);
            }
        }
        private void newObjTSMI_Click(object sender, EventArgs e) {
            var button = sender as ToolStripMenuItem;
            switch (button.Tag) {
                case "0":
                    Target obj = new Target(newObjectPos.Item1, newObjectPos.Item2, Color.Green);
                    director.Add(obj);
                    break;
                case "1":
                    Transporter r = new Transporter(newObjectPos.Item1, newObjectPos.Item2);
                    director.Add(r);
                    break;
                case "2":
                //Target @base = new Target(newObjectPos.Item1, newObjectPos.Item2, Color.Blue);
                //director.Add(@base);
                //break;
                default:
                    break;
            }
            mapPanel.Invalidate();
            newObjectPos = (0, 0);
        }
    }
}