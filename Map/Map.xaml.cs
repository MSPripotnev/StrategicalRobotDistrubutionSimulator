//#define PARALLEL
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
        static string currentFilePath = "";
        TimeSpan tempTime = new TimeSpan(0);
        int iterations = 0;
        Task[] directorTasks = new Task[2];
        DispatcherTimer refreshTimer;
        public MapWPF() {
            InitializeComponent();
            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = new TimeSpan(0,0,0,0,10);
            refreshTimer.Tick += RefreshTimer_Tick;
            currentFilePath = Properties.Resources.defaultFile;
            if (File.Exists(currentFilePath))
                OpenSaveFile(currentFilePath, true);
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e) {
            if (director.checkMission()) {
                director.Work();
                refreshTimer.Stop();
                startB.Content = "Запуск";
                for (int i = 0; i < menu.Items.Count; i++)
                    (menu.Items[i] as UIElement).IsEnabled = true;
            }
            Refresh();
#if PARALLEL
            director.Work();
#endif
        }
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

        DateTime startTime = DateTime.MinValue, pauseTime;
        Task mainTask;
        CancellationTokenSource tokenSource = new CancellationTokenSource();

        Point lastClickPos = new (0, 0);
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
                .Where(p => Analyzer.Distance(p.Position, pos) < 20)
                .MinBy(p => Analyzer.Distance(p.Position, pos));
            if (obj == null) {
                for (int i = 0; i < director.Obstacles.Length; i++)
                    if (director.Obstacles[i].PointOnObstacle(lastClickPos))
                        obj = director.Obstacles[i];
            }
            return obj;
        }
        private void OpenSaveFile(string path, bool open) {
            XmlSerializer serializer = new XmlSerializer(typeof(Director));
            if (open)
                using (FileStream fs = new FileStream(path, FileMode.Open)) {
                    director = serializer.Deserialize(fs) as Director;
                    director.Borders = mapCanvas.RenderSize;
                    if (director == null) return;
                    mapCanvas.Children.Clear();
                    DrawPlaceableObjects();
                    trajectoryScale_TextChanged(trajectoryScale, null);
                    startB.IsEnabled = true;
                    currentFilePath = path;
                } 
            else
                using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate)) {
                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Indent = true;
                    settings.IndentChars = "\t";
                    serializer.Serialize(XmlWriter.Create(fs, settings), director);
                }
        }
        private void Start() {
            for (int i = 0; i < menu.Items.Count; i++)
                (menu.Items[i] as UIElement).IsEnabled = false;
            tokenSource = new CancellationTokenSource();
            mainTask = new Task(() => {
                //director.DistributeTask();
                while (true) {
#if PARALLEL
                    director.DistributeTask();
#else
                    director.Work();
                    Dispatcher.Invoke(new Action(() => {
                        pauseTime = DateTime.Now;
                        director.DistributeTask();
                        tempTime -= DateTime.Now - pauseTime;
                    }));
#endif
                    if (tokenSource.IsCancellationRequested)
                        break;
                }
            }, tokenSource.Token, TaskCreationOptions.RunContinuationsAsynchronously);

            refreshTimer.Start();
            if (startTime == DateTime.MinValue)
                pauseTime = startTime;
            else
                tempTime += pauseTime - startTime;
            startTime = DateTime.Now;
            mainTask.Start();
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
                Start();
                stopB.IsEnabled = true;
                startB.Content = "Пауза";
            } else {
                pauseTime = DateTime.Now;
                refreshTimer.Stop();
                tokenSource.Cancel();
                startB.Content = "Запуск";
            }
        }
        private void stopB_Click(object sender, RoutedEventArgs e) {
            refreshTimer.Stop();
            tokenSource.Cancel();
            startB.Content = "Запуск";
            OpenSaveFile(currentFilePath, true);
            for (int i = 0; i < menu.Items.Count; i++)
                (menu.Items[i] as UIElement).IsEnabled = true;
            stopB.IsEnabled = false;
            tempTime = new TimeSpan(0);
            startTime = DateTime.Now;
            Refresh();
            startTime = DateTime.MinValue;
        }
        private void FileMenuItem_Click(object sender, RoutedEventArgs e) {
            var button = sender as MenuItem;
            switch (button.Tag) {
                case "0":
                    mapCanvas.Children.Clear();
                    director = new Director() { Borders = mapCanvas.RenderSize };
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

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {
            director.Borders = mapCanvas.RenderSize;
        }

        private void trajectoryScale_TextChanged(object sender, TextChangedEventArgs e) {
            if (director != null && float.TryParse((sender as TextBox).Text.Replace('.', ','), out float scale))
                director.Scale = scale;
        }

        public void Refresh() {
            collectedObjsCountL.Content = $"Количество собранных целей: {director.CollectedTargets.Length}";
            currentObjsCountL.Content = "Количество оставшихся целей: " + 
                (director.Targets.Length - director.CollectedTargets.Length).ToString();
            thinkTimeCountL.Content = $"Время расчёта: {Math.Round(director.ThinkingTime.TotalMilliseconds, 4)} ms";
            wayTimeCountL.Content = $"Время в пути: {Math.Round((DateTime.Now - startTime + tempTime).TotalSeconds, 2)} s";
            allTimeCountL.Content = $"Время алгоритма: {Math.Round((DateTime.Now - startTime + tempTime + director.ThinkingTime).TotalSeconds, 2)} s";
            //iterationsCountL.Content = "Количество итераций: " + iterations.ToString();
            iterationsCountL.Content = $"Пройденный путь: {Math.Round(director.TraversedWaySum, 1)}";
        }
    }
}
