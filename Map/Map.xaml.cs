//#define PARALLEL
using System.DirectoryServices.ActiveDirectory;
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

namespace TacticalAgro.Map {
    /// <summary>
    /// Логика взаимодействия для Map.xaml
    /// </summary>
    public partial class MapWPF : Window {
        Director director;
        TimeSpan tempTime = TimeSpan.Zero;
        DateTime startTime = DateTime.MinValue, pauseTime;
        DispatcherTimer refreshTimer;
        Tester tester= new Tester();

        public MapWPF() {
            InitializeComponent();
            refreshTimer = new DispatcherTimer {
                Interval = new TimeSpan(0, 0, 0, 0, 2)
            };
            tester = new Tester();
            refreshTimer.Tick += RefreshTimer_Tick;
            if (File.Exists(tester.Models[0].Path)) {
                OpenSaveFile(tester.Models[0].Path, true);
                transportersCountL.Content = $"Транспортеров: {director.Transporters.Length}";
            }
        }
        #region Drawing
        private void DrawPlaceableObjects() {
            for (int i = 0; i < director.AllObjectsOnMap.Count; i++) {
                IPlaceable obj = director.AllObjectsOnMap[i];
                var UIObj = obj.Build();
                mapCanvas.Children.Add(UIObj);

                if (obj is Transporter t) {
                    mapCanvas.Children.Add(t.BuildTrajectory());
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
            if (director == null) return null;
            var obj = director.AllObjectsOnMap
                .Where(p => PathFinder.Distance(p.Position, pos) < 20)
                .MinBy(p => PathFinder.Distance(p.Position, pos));
            if (obj == null) {
                for (int i = 0; i < director.Map.Obstacles.Length; i++)
                    if (director.Map.Obstacles[i].PointOnObstacle(lastClickPos))
                        obj = director.Map.Obstacles[i];
            }
            return obj;
        }
        private void CheckBox_Checked(object sender, RoutedEventArgs e) {
            if (director == null) return;
            if (drawCB.IsChecked ?? false) {
                DrawPlaceableObjects();
            } else {
                mapCanvas.Children.Clear();
                thinkTimeCountL.Content = "";
                wayTimeCountL.Content = "";
                allTimeCountL.Content = "";
            }
        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (director != null)
                director.Map.Borders = mapCanvas.RenderSize;
        }
        #endregion

        #region Work
        Task mainTask;
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        private double iterations = 0;
        private void RefreshTimer_Tick(object? sender, EventArgs e) {
            refreshTimer.Stop();
            iterations++;
            if (director.CheckMission()) {
                director.Work();
                if (testingCB.IsChecked == true) {
                    Stop();
                    tester.SaveResults(director, refreshTimer.Interval,
                    (DateTime.Now - startTime).TotalSeconds, ref iterations);
                    if (tester.NextAttempt()) {
                        OpenSaveFile(tester.Models[0].Path, true);

                        attemptsCountL.Content = $"Измерений осталось: {tester.AttemptsN}";
                        transportersCountL.Content = $"Транспортеров: {director.Transporters.Length}";
                        trajectoryScaleTB.Text = Math.Round(director.Scale, 3).ToString();

                        startButton_Click(sender, null);
                    } else {
                        director = null;
                        Refresh();
                        return;
                    }
                }
                else {
                    Pause();
                    Refresh();
                    return;
                }
            } /*else if ((DateTime.Now - startTime + tempTime).TotalSeconds > 180) {
                attemptsN = attemptsMax;
                Stop();
                director = null;
                OpenSaveFile(currentFilePath, true);
                trajectoryScaleTB.Text = Math.Round((director.Scale - 0.2F), 3).ToString();
                startButton_Click(sender, null);
            }*/
            Refresh();
            director.DistributeTask();
            director.Work();
            refreshTimer.Start();
        }
        private void Start() {
            for (int i = 0; i < menu.Items.Count; i++)
                (menu.Items[i] as UIElement).IsEnabled = false;
            tokenSource = new CancellationTokenSource();
            mainTask = new Task(() => {
                //director.DistributeTask();
                tokenSource.Token.Register(() => {
                    if (pauseTime == DateTime.MinValue) {
                        for (int i = 0; i < director.Transporters.Length; i++)
                            director.Transporters[i].Trajectory.Clear();
                    }
                });
                while (!tokenSource.Token.IsCancellationRequested) {
#if PARALLEL
                    director.DistributeTask();
#else
                    if (director != null) {
                        director.DistributeTask();
                        director.Work();
                    }
#endif
                }
            }, tokenSource.Token, TaskCreationOptions.LongRunning);

            refreshTimer.Start();

            startTime = pauseTime = DateTime.Now;
            //mainTask.Start();
        }
        private void Pause() {
            refreshTimer.Stop();
            tokenSource.Cancel();
            pauseTime = DateTime.Now;
            startB.Content = "Запуск";
            //pauseTime = startTime;
        }
        private void Stop() {
            tokenSource.Cancel();
            refreshTimer.Stop();
            RefreshTime();
            Refresh();
            OpenSaveFile(tester.Models[0].Path, true);
            startB.Content = "Запуск";
            for (int i = 0; i < menu.Items.Count; i++)
                (menu.Items[i] as UIElement).IsEnabled = true;
            stopB.IsEnabled = false;
        }
        private void RefreshTime() {
            if (director != null)
                director.ThinkingTime = TimeSpan.Zero;
            tempTime = TimeSpan.Zero;
            pauseTime = DateTime.MinValue;
            startTime = DateTime.Now;
        }
        private void trajectoryScale_TextChanged(object sender, TextChangedEventArgs e) {
            if (director != null && float.TryParse((sender as TextBox).Text.Replace('.', ','), out float scale))
                director.Scale = scale;
        }
        #endregion

        private void OpenSaveFile(string path, bool open) {
            XmlSerializer serializer = new XmlSerializer(typeof(Director));
            if (open) {
                director = tester.LoadModel(path);
                if (director == null) return;
                director.Map.Borders = mapCanvas.RenderSize;
                mapCanvas.Children.Clear();
                if (drawCB.IsChecked == true)
                    DrawPlaceableObjects();
                trajectoryScaleTB.Text = Math.Round(director.Scale, 3).ToString();
                startB.IsEnabled = true;
            } else
                using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate)) {
                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Indent = true;
                    settings.IndentChars = "\t";
                    serializer.Serialize(XmlWriter.Create(fs, settings), director);
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
            director.Add(obj);
            mapCanvas.Children.Add(obj.Build());
            if (obj is Transporter t) {
                mapCanvas.Children.Add(t.BuildTrajectory());
            }

            lastClickPos = new Point(0, 0);
        }
        private void startButton_Click(object sender, RoutedEventArgs e) {
            if (startB.Content.ToString() == "Запуск") {
                if (pauseTime > startTime)
                    tempTime += pauseTime - startTime;
                Start();
                pauseTime = startTime;
                stopB.IsEnabled = true;
                startB.Content = "Пауза";
            } else {
                startB.Content = "Запуск";
                Pause();
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
                    director = new Director() {
                        Map = new TacticalMap()
                    };
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
            director.Remove(obj);
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
                director.Add(obj);
                mapCanvas.Children.Add(obj.Build());
            }
            (mapCanvas.ContextMenu.Items[0] as MenuItem).IsEnabled = true;
            (mapCanvas.ContextMenu.Items[1] as MenuItem).IsEnabled = true;
            (mapCanvas.ContextMenu.Items[3] as MenuItem).IsEnabled = false;
        }
        #endregion

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            director.Dispose();
        }

        public void Refresh() {
            collectedObjsCountL.Content = $"Cобранных целей: {director.CollectedTargets.Length}";
            currentObjsCountL.Content = "Осталось целей: " +
                (director.Targets.Length - director.CollectedTargets.Length).ToString();
            traversedWayL.Content = $"Пройденный путь: {Math.Round(director.TraversedWaySum)}";
            if (drawCB.IsChecked == true) {
                thinkTimeCountL.Content = $"Время расчёта: {Math.Round(director.ThinkingTime.TotalMilliseconds)} ms";
                wayTimeCountL.Content = $"Время в пути: {(refreshTimer.Interval * iterations).TotalSeconds} s";
                allTimeCountL.Content = $"Время алгоритма: {Math.Round((DateTime.Now - startTime + tempTime).TotalSeconds, 3)} s";
            }
        }
    }
}
