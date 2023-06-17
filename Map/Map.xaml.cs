using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Serialization;

using TacticalAgro.Analyzing;
using TacticalAgro.Drones;

namespace TacticalAgro.Map {
    /// <summary>
    /// Логика взаимодействия для Map.xaml
    /// </summary>
    public partial class MapWPF : Window {
        private Director? director;
        public Director? Director {
            get => director;
            set {
                director = value;
                if (director != null) {
                    mapCanvas.Children.Clear();
                    if (drawCB.IsChecked == true)
                        DrawPlaceableObjects();
                    trajectoryScaleTB.Text = Math.Round(Director.Scale, 3).ToString();
                    startB.IsEnabled = true;
                }
            }
        }
        TimeSpan tempTime = TimeSpan.Zero;
        DateTime startTime = DateTime.MinValue, pauseTime;
        DispatcherTimer refreshTimer;
        Tester tester= new Tester();
        Recorder recorder = new Recorder();
        bool pause = true;
        private bool Pause {
            get => pause;
            set {
                if (pause == value) return;
                if (value) {
                    refreshTimer.Stop();
                    startB.Content = "▶️";
                    pauseTime = DateTime.Now;
                } else {
                    startB.Content = "||";
                    stopB.IsEnabled = true;
                    if (pauseTime != DateTime.MinValue)
                        tempTime += (pauseTime - startTime);
                    stepB.IsEnabled = true;
                    Start();
                }
                pause = value;
            }
        }

        public MapWPF() {
            InitializeComponent();
            refreshTimer = new DispatcherTimer {
                Interval = new TimeSpan(0,0,0,0,1)
            };
            tester = new Tester();
            recorder = new Recorder();
            tester.ModelSwitched += recorder.OnModelSwitched;
            tester.ModelSwitched += OnModelSwitched;
            tester.AttemptStarted += OnAttemptStarted;
            tester.AttemptCompleted += OnAttemptCompleted;
            tester.AttemptFailed += OnAttemptFailed;
            refreshTimer.Tick += RefreshTimer_Tick;

            if ((tester?.Models?.Any() == true) && File.Exists(tester.Models[0].Path)) {
                Director = tester.LoadModel(tester.Models[0].Path);
                attemptsCountL.Content = $"Измерений осталось: {tester.AttemptsN}\n" +
                    $"Транспортеров: {Director.Transporters.Count}\n" +
                    $"Моделей: {tester.Models.Length}";
            }
        }
        #region Drawing
        private void DrawPlaceableObjects() {
            for (int i = 0; i < Director.AllObjectsOnMap.Count; i++) {
                IPlaceable obj = Director.AllObjectsOnMap[i];
                var UIObj = obj.Build();
                mapCanvas.Children.Add(UIObj);

                if (obj is Transporter t) {
                    mapCanvas.Children.Add(t.BuildTrajectory());
#if DRAW_SEARCH_AREA
                    mapCanvas.Children.Add(t.PointsAnalyzed(true));
                    mapCanvas.Children.Add(t.PointsAnalyzed(false));
#endif
                }
            }
        }
        Point lastClickPos = new(0, 0);
        private Polygon? new_obstacle = null;
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) {
            Point clickPos = e.GetPosition(this);
            //if (e.RightButton == MouseButtonState.Pressed) {
            lastClickPos = new Point(Math.Round(clickPos.X, 2),
                                     Math.Round(clickPos.Y, 2));
            //}
            if (e.LeftButton == MouseButtonState.Pressed) {
                if (new_obstacle != null) {
                    mapCanvas.Children.Remove(new_obstacle);
                    PointCollection ps = new_obstacle.Points;
                    ps.Add(clickPos);
                    new_obstacle = new Polygon {
                        Fill = new SolidColorBrush(Colors.DarkSlateGray),
                        Points = ps
                    };
                    mapCanvas.Children.Add(new_obstacle);
                } else {
                    IPlaceable? obj = FindObject(clickPos);
                    if (obj != null) {
                        Binding binding = new Binding();
                        binding.Source = obj;
                        binding.Mode = BindingMode.TwoWay;
                        propertyGrid.SelectedObject = binding.Source;
                    } else propertyGrid.SelectedObject = null;
                }
            }
        }
        private IPlaceable? FindObject(Point pos) {
            if (Director == null) return null;
            var obj = Director.AllObjectsOnMap
                .Where(p => PathFinder.Distance(p.Position, pos) < 20)
                .MinBy(p => PathFinder.Distance(p.Position, pos));
            if (obj == null) {
                for (int i = 0; i < Director.Map.Obstacles.Length; i++)
                    if (Director.Map.Obstacles[i].PointOnObstacle(lastClickPos))
                        obj = Director.Map.Obstacles[i];
            }
            return obj;
        }
        private void CheckBox_Checked(object sender, RoutedEventArgs e) {
            if (Director == null) return;
            if (drawCB.IsChecked ?? false) {
                DrawPlaceableObjects();
            } else {
                mapCanvas.Children.Clear();
                thinkTimeCountL.Content = "";
                wayTimeCountL.Content = "";
                allTimeCountL.Content = "";
            }
            nextModelB.IsEnabled = testingCB.IsChecked == true;
        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (Director != null)
                Director.Map.Borders = mapCanvas.RenderSize;
        }
        #endregion

        #region Work
        Task mainTask;
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        private double iterations = 0;
        TimeSpan realWorkTime = TimeSpan.Zero, realWayTime = TimeSpan.Zero;
        private void OnAttemptStarted(object? sender, EventArgs e) {
            Director = tester.ReloadModel();

            attemptsCountL.Content = $"Измерений осталось: {tester.AttemptsN}\n" +
                $"Транспортеров: {Director.Transporters.Count}\n" +
                $"Моделей: {tester.Models.Length}";
            trajectoryScaleTB.Text = Math.Round(Director.Scale, 3).ToString();

            startButton_Click(sender, null);
        }
        private void OnAttemptCompleted(object? sender, EventArgs e) {
            realWorkTime += (DateTime.Now - startTime);
            recorder.SaveResults(Director, tester.Models[0].Name, realWorkTime, ref iterations);
            Stop();
            Director = null;
        }
        private void OnAttemptFailed(object? sender, EventArgs e) {
            OnAttemptCompleted(sender, e);
        }
        private void OnModelSwitched(object? sender, EventArgs e) {
            if (sender == null) {
                Director = null;
            } else {
                Director = tester.ReloadModel();
                attemptsCountL.Content = $"Измерений осталось: {tester.AttemptsN}\n" +
                    $"Транспортеров: {Director.Transporters.Count}\n" +
                    $"Моделей: {tester.Models.Length}";
                nextModelB.IsEnabled = tester.Models.Length > 1;
            }
        }
        private void RefreshTimer_Tick(object? sender, EventArgs e) {
            refreshTimer.Stop();
            var dt = DateTime.Now;
            Work();
            realWayTime += (DateTime.Now - dt);
            if (Director != null && !Director.CheckMission())
                refreshTimer.Start();
        }
        private void Work() {
            try {
                if (Director == null) return;
                Director.DistributeTask();
                Director.Work();
                if (Director.CheckMission()) {
                    Director.Work();
                    if (testingCB.IsChecked == true) {
                        tester.NextAttempt();
                        return;
                    } else {
                        stopB.IsEnabled = false;
                        Pause = true;
                        Refresh();
                        return;
                    }
                } else if (realWayTime.TotalSeconds > 60) {
                    tester.StopAttempt();
                }
            } catch (Exception ex) {
                MessageBox.Show(ex.ToString());
                recorder.Dispose();
                Director.Dispose();
            }
            Refresh();
        }
        private void Start() {
            for (int i = 0; i < menu.Items.Count; i++)
                (menu.Items[i] as UIElement).IsEnabled = false;
            tokenSource = new CancellationTokenSource();
#if PARALLEL
            mainTask = new Task(() => {
                //director.DistributeTask();
                tokenSource.Token.Register(() => {
                    if (pauseTime == DateTime.MinValue) {
                        for (int i = 0; i < director.Transporters.Length; i++)
                            director.Transporters[i].Trajectory.Clear();
                    }
                });
                while (!tokenSource.Token.IsCancellationRequested) {
                    director.DistributeTask();
                    if (director != null) {
                        Dispatcher.Invoke(() => {
                            Work();
                        });
                    }
                }
            }, tokenSource.Token, TaskCreationOptions.LongRunning);
#endif
            if (Director.CheckMission()) {
                Stop();
                Director = tester.ReloadModel();
            }
            refreshTimer.Start();

            startTime = DateTime.Now;
            //mainTask.Start();
        }
        private void Stop() {
            //tokenSource.Cancel();
            if (testingCB.IsChecked == false)
                Director = tester.ReloadModel();
            refreshTimer.Stop();
            RefreshTime();
            Refresh();
            startB.Content = "▶️";
            for (int i = 0; i < menu.Items.Count; i++)
                (menu.Items[i] as UIElement).IsEnabled = true;
            stopB.IsEnabled = false;
            stepB.IsEnabled = false;
        }
        private void RefreshTime() {
            tempTime = TimeSpan.Zero;
            realWorkTime = TimeSpan.Zero;
            realWayTime = TimeSpan.Zero;
            pauseTime = DateTime.MinValue;
            startTime = DateTime.Now;
        }
        private void trajectoryScale_TextChanged(object sender, TextChangedEventArgs e) {
            if (Director != null && float.TryParse((sender as TextBox).Text.Replace('.', ','), out float scale))
                Director.Scale = scale;
        }
        #endregion

        private void OpenSaveFile(string path, bool open) {
            XmlSerializer serializer = new XmlSerializer(typeof(Director));
            if (open) {
                using (FileStream fs = new FileStream(path, FileMode.Open)) {
                    Director = serializer.Deserialize(fs) as Director;
                    fs.Close();
                }
            } else
                using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate)) {
                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Indent = true;
                    settings.IndentChars = "\t";
                    serializer.Serialize(XmlWriter.Create(fs, settings), Director);
                    fs.Close();
                }
        }

        #region Buttons
        private void MenuItem_Click(object sender, RoutedEventArgs e) {
            var button = sender as MenuItem;
            IPlaceable obj = null;
            switch (button.Tag) {
                case "0":
                    obj = new Target(lastClickPos);
                    break;
                case "1":
                    obj = new Transporter(lastClickPos);
                    break;
                case "3":
                    obj = new Base(lastClickPos);
                    break;
                case "4":
                    if (new_obstacle == null) {
                        new_obstacle = new Polygon();
                        new_obstacle.StrokeThickness = 2;
                        new_obstacle.Fill = new SolidColorBrush(Colors.DarkSlateGray);
                        new_obstacle.Points = new PointCollection(new Point[] { lastClickPos });
                        mapCanvas.Children.Add(new_obstacle);
                        Ellipse el = new Ellipse();
                        el.Width = el.Height = 5;
                        el.Margin = new Thickness(lastClickPos.X, lastClickPos.Y, 0, 0);
                        el.Fill = new SolidColorBrush(Colors.DarkSlateGray);
                        mapCanvas.Children.Add(el);

                        (mapCanvas.ContextMenu.Items[0] as MenuItem).IsEnabled = false;
                        (mapCanvas.ContextMenu.Items[1] as MenuItem).IsEnabled = false;
                        (mapCanvas.ContextMenu.Items[3] as MenuItem).IsEnabled = true;
                        (mapCanvas.ContextMenu.Items[3] as MenuItem).Tag = button.Tag;
                        return;
                    } else {

                    }
                    break;
                case "5":
                    obj = new Base(lastClickPos);
                    break;
                default:
                    break;
            }
            if (obj == null) return;
            Director.Add(obj);
            mapCanvas.Children.Add(obj.Build());
            if (obj is Transporter t) {
                mapCanvas.Children.Add(t.BuildTrajectory());
            }

            lastClickPos = new Point(0, 0);
        }
        private void startButton_Click(object sender, RoutedEventArgs e) {
            if (startB.Content.ToString() == "▶️") {
                if (pauseTime > startTime)
                    tempTime += pauseTime - startTime;
                Start();
                pauseTime = startTime;
                stopB.IsEnabled = true;
                startB.Content = "||";
                Pause = false;
            } else {
                startB.Content = "▶️";
                Pause = true;
            }
        }
        private void stopB_Click(object sender, RoutedEventArgs e) {
            Stop();
        }
        private void FileMenuItem_Click(object sender, RoutedEventArgs e) {
            var button = sender as MenuItem;
            switch (button.Tag) {
                case "0":
                    mapCanvas.Children.Clear();
                    Director = new Director();
                    break;
                case "1": //открыть
                    Microsoft.Win32.OpenFileDialog oFD = new Microsoft.Win32.OpenFileDialog();
                    oFD.Filter = "Файлы разметки поля|*.xml|Все файлы|*.*";
                    if (oFD.ShowDialog() == true && oFD.FileName != "")
                        OpenSaveFile(oFD.FileName, true);
                    break;
                case "2": //сохранить
                    Microsoft.Win32.SaveFileDialog sFD = new Microsoft.Win32.SaveFileDialog();
                    sFD.Filter = "Файлы разметки поля|*.xml|Все файлы|*.*";
                    if (sFD.ShowDialog() == true) {
                        OpenSaveFile(sFD.FileName, false);
                    }
                    break;
                default:
                    break;
            }
        }
        private void deleteObjectB_Click(object sender, RoutedEventArgs e) {
            IPlaceable? obj = null;
            obj = FindObject(lastClickPos);

            if (obj == null) return;
            if (propertyGrid.SelectedObject == obj)
                propertyGrid.SelectedObject = null;
            Director.Remove(obj);
            mapCanvas.Children.Clear();

            DrawPlaceableObjects();
        }
        private void undoB_Click(object sender, RoutedEventArgs e) {
            if (new_obstacle != null) {
                if (new_obstacle.Points.Count > 1)
                    new_obstacle.Points.RemoveAt(new_obstacle.Points.Count - 1);
                else {
                    new_obstacle = null;
                }
            } else {
                mapCanvas.Children.Remove(mapCanvas.Children[^1]);

            }
        }
        private void finishObjectB_Click(object sender, RoutedEventArgs e) {
            IPlaceable obj = null;
            if (finishObjectB.Tag.ToString() == "4") {
                mapCanvas.Children.Remove(new_obstacle);
                mapCanvas.Children.Remove(mapCanvas.Children[^1]);
                obj = new Obstacle(new_obstacle.Points.ToArray());
                new_obstacle = null;
            }
            if (obj != null) {
                Director.Add(obj);
                mapCanvas.Children.Add(obj.Build());
            }
            (mapCanvas.ContextMenu.Items[0] as MenuItem).IsEnabled = true;
            (mapCanvas.ContextMenu.Items[1] as MenuItem).IsEnabled = true;
            (mapCanvas.ContextMenu.Items[3] as MenuItem).IsEnabled = false;
        }
        #endregion

        private void nextModelB_Click(object sender, RoutedEventArgs e) {
            tester.NextModel();
        }

        private void stepB_Click(object sender, RoutedEventArgs e) {
            refreshTimer.Stop();
            var dt = DateTime.Now;
            Work();
            var ts = DateTime.Now - dt;
            tempTime -= DateTime.Now - pauseTime; Refresh();
            tempTime += DateTime.Now - pauseTime;
            realWayTime += ts;
        }

        private void testsB_Click(object sender, RoutedEventArgs e) {
            Analyzing.Tests.TestsWindow window = new Analyzing.Tests.TestsWindow();
            window.ShowDialog();
            tester.LoadModels();
            if (File.Exists(tester.Models[0].Path))
                Director = tester.LoadModel(tester.Models[0].Path);
            }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            Director.Dispose();
            recorder.Dispose();
        }

        public void Refresh() {
            if (Director != null) {
                collectedObjsCountL.Content = $"Cобранных целей: {Director.CollectedTargets.Length}";
                currentObjsCountL.Content = "Осталось целей: " +
                    (Director.Targets.Length - Director.CollectedTargets.Length).ToString();
                traversedWayL.Content = $"Пройденный путь: {Math.Round(Director.TraversedWaySum)} px";
                if (drawCB.IsChecked == true) {
                    thinkTimeCountL.Content = $"Сложность расчёта: {Director.ThinkingIterations} it";
                    if (Director.Transporters.Any())
                        wayTimeCountL.Content = $"Время в пути: {Director.WayIterations} it";
                    allTimeCountL.Content = $"Время алгоритма: {Math.Round((DateTime.Now - startTime).TotalSeconds, 3)} s";
                }
            }
        }
    }
}
