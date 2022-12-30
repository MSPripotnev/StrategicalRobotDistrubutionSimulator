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
        TimeSpan tempTime = TimeSpan.Zero;
        DateTime startTime = DateTime.MinValue, pauseTime;
        DispatcherTimer refreshTimer;
        List<Reading> readings = new List<Reading>();
        [XmlArray(ElementName = "Readings")]
        [XmlArrayItem(ElementName = "Reading")]
        public Reading[] Readings {
            get {
                return readings.ToArray();
            }
            set {
                readings = new List<Reading>(value);
            }
        }
        const int attemptsMax = 50;
        float scaleMax = 40F;
        int attemptsN = attemptsMax;
        int transportersCountT = 5 - 1;

        public MapWPF() {
            InitializeComponent();
            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = new TimeSpan(0,0,0,0,10);
            refreshTimer.Tick += RefreshTimer_Tick;
            currentFilePath = modelsFiles[0];
            if (File.Exists(currentFilePath)) {
                OpenSaveFile(currentFilePath, true);
                transportersCountL.Content = $"Транспортеров: {director.Transporters.Length}";
                director.Scale = scaleMax;
                trajectoryScaleTB.Text = scaleMax.ToString();
            }
            for (int i = 0; i < modelsFiles.Length; i++) {
                string resFileName = $"Results{modelsFiles[i].Substring(modelsFiles[i].Length-4-4)}.xml";
                if (!File.Exists(resFileName)) {
                    using (FileStream fs = new FileStream(resFileName, FileMode.Create)) {
                        XmlWriterSettings settings = new XmlWriterSettings() {
                            Indent = true,
                            ConformanceLevel = ConformanceLevel.Auto,
                            WriteEndDocumentOnClose = false
                        };
                        var writer = XmlWriter.Create(fs, settings);
                        writer.WriteStartDocument();
                        writer.WriteStartElement("Readings");
                        writer.Close();
                    }
                }
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
                for (int i = 0; i < director.Obstacles.Length; i++)
                    if (director.Obstacles[i].PointOnObstacle(lastClickPos))
                        obj = director.Obstacles[i];
            }
            return obj;
        }
        private void CheckBox_Checked(object sender, RoutedEventArgs e) {
            if (director == null) return;
            if (drawCB.IsChecked ?? false) {
                DrawPlaceableObjects();
            } else {
                mapCanvas.Children.Clear();
            }
        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (director != null)
                director.Borders = mapCanvas.RenderSize;
        }
        #endregion

        #region Work
        Task mainTask;
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        string[] modelsFiles = new string[] {
            "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside1-10.xml",
            "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside1-20.xml",
            "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside2-10.xml",
            "G:\\GosPlan\\Arbeiten\\Kreation\\Diplom\\TacticalAgro\\testInside2-20.xml"
        };
        private double iterations = 0;
        private void RefreshTimer_Tick(object? sender, EventArgs e) {
            refreshTimer.Stop();
            iterations++;
            if (director.CheckMission()) {
                director.Work();
                if (testingCB.IsChecked == true) {
                    SaveResults();
                    Stop();
                    if (--attemptsN < 1) {
                        SaveResults(Readings);
                        readings.Clear();
                        if (director.Scale > 1.0F) {
                            trajectoryScaleTB.Text = Math.Round((director.Scale-1F), 3).ToString();
                            attemptsN = attemptsMax;
                            director = null;
                            OpenSaveFile(currentFilePath, true);
                            Thread.Sleep(200);
                            startButton_Click(sender, null);
                        } else if (modelsFiles.Any()) {
                            currentFilePath = modelsFiles[0];
                            modelsFiles = modelsFiles.Skip(1).ToArray();
                            attemptsN = attemptsMax;
                            director = null;
                            OpenSaveFile(currentFilePath, true);
                            trajectoryScaleTB.Text = scaleMax.ToString();
                            Thread.Sleep(200);
                            startButton_Click(sender, null);
                        }
                    } else {
                        director = null;
                        OpenSaveFile(currentFilePath, true);
                        Thread.Sleep(100);
                        startButton_Click(sender, null);
                    }
                    attemptsCountL.Content = $"Измерений осталось: {attemptsN}";
                } 
                else {
                    Pause();
                    Refresh();
                    return;
                }
            } else if ((DateTime.Now - startTime + tempTime).TotalSeconds > 60) {
                attemptsN = attemptsMax;
                Stop();
                director = null;
                OpenSaveFile(currentFilePath, true);
                trajectoryScaleTB.Text = Math.Round((director.Scale - 0.2F), 3).ToString();
                startButton_Click(sender, null);
            }
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
            OpenSaveFile(currentFilePath, true);
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
            if (open)
                using (FileStream fs = new FileStream(path, FileMode.Open)) {
                    director = serializer.Deserialize(fs) as Director;
                    if (director == null) return;
                    var @base = director.Bases[0];
                    Transporter[] transporters = new Transporter[transportersCountT];
                    for (int i = 0; i < transporters.Length; i++) {
                        transporters[i] = new Transporter(@base.Position);
                        director.Add(transporters[i]);
                    }
                    director.Borders = mapCanvas.RenderSize;
                    mapCanvas.Children.Clear();
                    if (drawCB.IsChecked == true)
                        DrawPlaceableObjects();
                    trajectoryScale_TextChanged(trajectoryScaleTB, null);
                    startB.IsEnabled = true;
                    if (currentFilePath != path)
                        currentFilePath = path;
                    fs.Close();
                } 
            else
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

        #region Analyzing
        private void SaveResults() {
            var analyzer = new Reading() {
                Scale = director.Scale,
                TransportersCount = director.Transporters.Length,
                CalcTime = Math.Round(director.ThinkingTime.TotalMilliseconds),
                WayTime = iterations*refreshTimer.Interval.TotalSeconds,//Math.Round((DateTime.Now - startTime + tempTime - director.ThinkingTime).TotalSeconds, 3),
                FullTime = Math.Round((DateTime.Now - startTime + tempTime).TotalSeconds, 3),
                TraversedWay = Math.Round(director.TraversedWaySum),
                STransporterWay = new double[director.Transporters.Length],
                TargetsCount = director.Targets.Length,
                Iterations = Math.Round(iterations)
            };
            analyzer.RandomTime = analyzer.FullTime - analyzer.WayTime;
            if (director.Transporters.Any()) {
                analyzer.TransportersSpeed = Math.Round(director.Transporters[0].Speed, 8);
                for (int i = 0; i < director.Transporters.Length; i++)
                    analyzer.STransporterWay[i] = director.Transporters[i].TraversedWay;
            }
            readings.Add(analyzer);
            iterations = 0;
        }
        private void SaveResults(Reading[] _readings) {
            string resFileName = $"Results{currentFilePath.Substring(currentFilePath.Length -4 - 4)}.xml";
            XmlSerializer serializer = new XmlSerializer(typeof(Reading));
            using (FileStream fs = new FileStream(resFileName, FileMode.Append)) {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration= true;
                settings.Indent = true;
                settings.IndentChars = "\t";
                XmlWriter xmlWriter= XmlWriter.Create(fs, settings);
                for (int i = 0; i < _readings.Length; i++)
                    serializer.Serialize(xmlWriter, _readings[i], null);
            }
        }
        #endregion

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            director.Dispose();
        }

        public void Refresh() {
            collectedObjsCountL.Content = $"Cобранных целей: {director.CollectedTargets.Length}";
            currentObjsCountL.Content = "Осталось целей: " + 
                (director.Targets.Length - director.CollectedTargets.Length).ToString();
            thinkTimeCountL.Content = $"Время расчёта: {Math.Round(director.ThinkingTime.TotalMilliseconds)} ms";
            wayTimeCountL.Content = $"Время в пути: {Math.Round((DateTime.Now - startTime + tempTime - director.ThinkingTime).TotalSeconds, 3)} s";
            allTimeCountL.Content = $"Время алгоритма: {Math.Round((DateTime.Now - startTime + tempTime).TotalSeconds, 3)} s";
            traversedWayL.Content = $"Пройденный путь: {Math.Round(director.TraversedWaySum)}";
        }
    }
}
