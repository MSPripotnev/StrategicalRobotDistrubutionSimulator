namespace TacticalAgro {
    public partial class Form1 : Form {
        Director director;
        double workTime = 0;
        int iterations = 0;
        public Form1() {
            InitializeComponent();
            Robot[] robots = new Robot[] {
                new Robot(50, 150),
                new Robot(150, 250),
                new Robot(250, 350)
            };
            Target[] targets = new Target[] {
                new Target(650, 250, Color.Green),
                new Target(350, 250, Color.Green),
                new Target(250, 250, Color.Green),
                new Target(550, 250, Color.Green),
            };
            Target @base = new Target(650, 450, Color.Blue);
            director = new Director(robots, targets, @base);
            director.DistributeTask();
        }

        private void refreshTimer_Tick(object sender, EventArgs e) {

            director.DistributeTask();
            director.Work();
            workTime += refreshTimer.Interval;
            iterations++;

            currentObjsCountL.Text = (director.Targets.Length - director.CollectedTargets.Length).ToString();
            collectedObjsCountL.Text = director.CollectedTargets.Length.ToString();
            timeCountL.Text = Math.Round(workTime / 1000, 6).ToString() + " s";
            iterationsCountL.Text = iterations.ToString();

            mapPanel.Invalidate();

            if (director.checkMission()) {
                refreshTimer.Stop();
                startB.Text = "Запуск";
                return;
            }
        }

        const int standartRobotSize = 30;
        const int standartObjectSize = 15;
        private void mapPanel_Paint(object sender, PaintEventArgs e) {
            for (int i = 0; i < director.AllObjectsOnMap.Count; i++) {
                IMoveable obj = director.AllObjectsOnMap[i];
                Pen pen = new Pen(obj.Color, 5);
                if (obj is Robot)
                    e.Graphics.DrawEllipse(pen,
                        obj.Position.X, obj.Position.Y,
                        standartRobotSize, standartRobotSize);
                else if (obj is Target)
                    e.Graphics.DrawEllipse(pen,
                        obj.Position.X + standartObjectSize / 2, obj.Position.Y + standartObjectSize / 2,
                        standartObjectSize, standartObjectSize);
            }
        }

        private void startB_Click(object sender, EventArgs e) {
            if (startB.Text == "Запуск") {
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
                    Robot r = new Robot(newObjectPos.Item1, newObjectPos.Item2);
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