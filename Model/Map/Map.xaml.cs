#define ALWAYS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SRDS.Model.Map;
using Analyzing;
using Analyzing.Models;
using Direct;
using Direct.Agents;
using Direct.Agents.Drones;
using Direct.Strategical.Qualifiers;
using Direct.Tactical;
using Environment;
using Stations;
using Targets;
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
                if (director.Meteo != null)
                    director.Meteo.PropertyChanged += RefreshMeteo;
                director.PropertyChanged += RefreshMeteo;
                director.Map.Borders = mapCanvas.RenderSize;

                director.Seed = 0;
                if (!tester.Models.Any()) {
                    var vs = tester.Models.ToList();
                    vs.Add(new CopyModel(director));
                    tester.Models = vs.ToArray();
                }
                tester.ModelSwitched += director.Recorder.OnModelSwitched;
            }
        }
    }
    TimeSpan tempTime = TimeSpan.Zero;
    DateTime startTime = DateTime.MinValue, pauseTime, d_time = new DateTime(0);
    DispatcherTimer refreshTimer;
    Tester tester = new Tester();
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
                if (testingCB.IsChecked == true)
                    meteoCB.IsEnabled = false;
                pauseTime = startTime;
                stopB.IsEnabled = true;
                if (pauseTime > startTime)
                    tempTime += pauseTime - startTime;
                Start();
            }
            pause = value;
        }
    }

    public MapWPF() {
        InitializeComponent();
        refreshTimer = new DispatcherTimer {
            Interval = new TimeSpan(0, 0, 0, 0, 1)
        };
        tester = new Tester();
        tester.ModelSwitched += OnModelSwitched;
        tester.AttemptStarted += OnAttemptStarted;
        tester.AttemptCompleted += OnAttemptCompleted;
        tester.AttemptFailed += OnAttemptFailed;
        refreshTimer.Tick += RefreshTimer_Tick;

        if ((tester?.Models?.Any() == true) && File.Exists(tester.Models[0].Path)) {
            try {
                Director = tester.Models[0].Unpack();
                attemptsCountL.Content = $"Измерений осталось: {tester.AttemptsN}\n" +
                    $"Транспортеров: {Director.Agents.Length}\n" +
                    $"Моделей: {tester.Models.Length}";
            } catch (FileNotFoundException ex) {
                mapCanvas.Children.Clear();
                Director = new Director(mapCanvas.RenderSize);
            }
        } else {
            Director = new Director(mapCanvas.RenderSize);
        }
    }

    #region Drawing
    private void RefreshMeteo(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (drawCB.IsChecked != true)
            return;
        if (e.PropertyName == nameof(Director.Targets) || e.PropertyName == nameof(Director.Meteo.GeneratedSnowdrifts)) {
            for (int i = 0; i < mapCanvas.Children.Count; i++)
                if (mapCanvas.Children[i].Uid == nameof(Snowdrift))
                    mapCanvas.Children.Remove(mapCanvas.Children[i]);
            foreach (Snowdrift s in Director.Targets.OfType<Snowdrift>())
                mapCanvas.Children.Add(s.Build());
            return;
        }
        if (Director.Meteo != null && e.PropertyName == nameof(Director.Meteo.Clouds)) {
            for (int i = 0; i < mapCanvas.Children.Count; i++)
                if (mapCanvas.Children[i].Uid == nameof(SnowCloud))
                    mapCanvas.Children.Remove(mapCanvas.Children[i]);
            foreach (SnowCloud o in Director.Meteo.Clouds)
                mapCanvas.Children.Add(o.Build());
        }
    }
    private void DrawPlaceableObjects() {
        var objs = Director.AllObjectsOnMap.Concat(Director.Map.Roads).ToArray();
        if (Director.Meteo != null)
            objs = objs.Concat(Director.Meteo.Clouds).ToArray();
        for (int i = 0; i < objs.Length; i++) {
            IPlaceable obj = objs[i];
            var UIObj = obj.Build();
            mapCanvas.Children.Add(UIObj);

            if (obj is IPlaceableWithArea obja)
                mapCanvas.Children.Add(obja.BuildArea());

            if (obj is Agent t) {
                mapCanvas.Children.Add(t.BuildTrajectory());
#if DRAW_SEARCH_AREA
                mapCanvas.Children.Add(t.PointsAnalyzed(true));
                mapCanvas.Children.Add(t.PointsAnalyzed(false));
#endif
            }
        }
    }
    Point lastClickPos = new(0, 0);
    Point prevLastClickPos = new(0, 0);
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
            } else if (prevLastClickPos.X != 0 || prevLastClickPos.Y != 0) {
                Road r = new Road(prevLastClickPos, lastClickPos, 1, Director.Map.Roads.ToArray());
                Director.Add(r);
                mapCanvas.Children.Remove(mapCanvas.Children[^1]);

                foreach (var cr in Director.Map.Crossroads) {
                    UIElement el = cr.Build();
                    if (mapCanvas.Children.Contains(el))
                        mapCanvas.Children.Remove(el);
                    mapCanvas.Children.Add(el);
                }
                mapCanvas.Children.Add(r.Build());

                if (!Keyboard.IsKeyDown(Key.LeftShift)) {
                    prevLastClickPos = new Point(0, 0);
                    (mapCanvas.ContextMenu.Items[0] as MenuItem).IsEnabled = true;
                    (mapCanvas.ContextMenu.Items[1] as MenuItem).IsEnabled = true;
                    (mapCanvas.ContextMenu.Items[3] as MenuItem).IsEnabled = false;
                    return;
                }
                prevLastClickPos = lastClickPos;
                Rectangle crossroad_r = new Rectangle() {
                    Width = Height = 10,
                    Margin = new Thickness(prevLastClickPos.X, prevLastClickPos.Y, 0, 0),
                    Fill = new SolidColorBrush(Colors.DarkSlateGray),
                };
                mapCanvas.Children.Add(crossroad_r);
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
        obj ??= Director.Map.Roads
                .Where(p => 0 < p.DistanceToRoad(pos) && p.DistanceToRoad(pos) < 10)
                .MinBy(p => p.DistanceToRoad(pos));
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
            systemQualityL.Content = "";
            bestQualityL.Content = "";
            wayTimeCountL.Content = "";
            allTimeCountL.Content = "";
        }
        meteoCB.IsEnabled = !(nextModelB.IsEnabled = testingCB.IsChecked == true);
    }
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {
        if (Director != null)
            Director.Map.Borders = mapCanvas.RenderSize;
    }
    #endregion

    #region Work
    Task mainTask;
#if PARALLEL
    CancellationTokenSource tokenSource = new CancellationTokenSource();
#endif
    private double iterations = 0;
    TimeSpan realWorkTime = TimeSpan.Zero, realWayTime = TimeSpan.Zero;
    private void OnAttemptStarted(object? sender, EventArgs e) {
        Director = tester.ReloadModel();

        attemptsCountL.Content = $"Измерений осталось: {tester.AttemptsN}\n" +
            $"Транспортеров: {Director.Agents.Length}\n" +
            $"Моделей: {tester.Models.Length}";
        trajectoryScaleTB.Text = Math.Round(Director.Scale, 3).ToString();

        startButton_Click(sender, null);
    }
    private void OnAttemptCompleted(object? sender, EventArgs e) {
        realWorkTime += (DateTime.Now - startTime);
        Director.Recorder.SaveResults(Director, tester.Models[0].Name, realWorkTime, ref iterations);
        Director.Learning.Select(Director.Recorder);
        if (Director.Distributor.Qualifier is FuzzyQualifier fq)
            Director.Learning.Mutate(ref fq.Net);
        Director.Distributor.DistributionQualifyReadings = new();
        tester.ActiveDirector = Director;
        Stop();
    }
    private void OnAttemptFailed(object? sender, EventArgs e) {
        OnAttemptCompleted(sender, e);
    }
    private void OnModelSwitched(object? sender, EventArgs e) {
        if (sender == null) {
            Director = null;
        } else {
            if (Director is not null)
                Director.Recorder = new Recorder();
            Director = tester.ReloadModel();
            attemptsCountL.Content = $"Измерений осталось: {tester.AttemptsN}\n" +
                $"Транспортеров: {Director.Agents.Length}\n" +
                $"Моделей: {tester.Models.Length}";
            nextModelB.IsEnabled = tester.Models.Length > 1;
        }
    }
    private void RefreshTimer_Tick(object? sender, EventArgs e) {
        refreshTimer.Stop();
        d_time = d_time.AddMinutes(1);
        var dt = DateTime.Now;
        Work();
        realWayTime += (DateTime.Now - dt);
        if (tester.Models[0].AttemptTime is not null && d_time >= tester.Models[0].AttemptTime) {
            if (testingCB.IsChecked == true) {
                tester.NextAttempt();
                return;
            } else {
                menu.IsEnabled = true;
                stopB.IsEnabled = false;
                Pause = true;
                Refresh();
                return;
            }
        }
        if (Director != null && !Director.CheckMission())
            refreshTimer.Start();
        if (propertyGrid.SelectedObject != null) {
            object o = propertyGrid.SelectedObject;
            propertyGrid.SelectedObject = null;
            propertyGrid.SelectedObject = o;
        }
    }
    private void Work() {
        try {
            if (Director == null) return;
            Director.Work(d_time);
#if !ALWAYS
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
#endif
        } catch (Exception ex) {
            MessageBox.Show(ex.ToString());
            Director.Recorder.Dispose();
            Director.Dispose();
        }
        Refresh();
    }
    private void Start() {
        for (int i = 0; i < menu.Items.Count; i++)
            (menu.Items[i] as UIElement).IsEnabled = false;
#if PARALLEL
			tokenSource = new CancellationTokenSource();
        mainTask = new Task(() => {
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
#if !ALWAYS
        if (Director.CheckMission()) {
            Stop();
            Director = tester.ReloadModel();
            startTime = DateTime.Now;
        }
#endif
#if PARALLEL
        mainTask.Start();
#else
        refreshTimer.Start();
#endif
    }
    private void Stop() {
#if PARALLEL
        tokenSource.Cancel();
#endif
        if (testingCB.IsChecked == false && tester.Models?.Length < 2) {
            Director = tester.ReloadModel();
            meteoCB.IsEnabled = true;
        }
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
        d_time = new DateTime(0);
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

    #region Buttons
    double lastSpeed = 1;
    private void MenuItem_Click(object sender, RoutedEventArgs e) {
        var button = sender as MenuItem;
        IPlaceable obj = null;
        switch (button.Tag) {
            case "01":
            obj = new Crop(lastClickPos);
            if (!Director.Map.Stations.Any(p => p is CollectingStation))
                return;
            break;
            case "02":
            obj = new Snowdrift(lastClickPos, new Random().NextDouble() * 10, new Random());
            break;
            case "11":
            case "12":
            case "13":
            if (button.Tag.ToString() == "11")
                obj = new Transporter(lastClickPos);
            else if (button.Tag.ToString() == "12") {
                List<SnowRemoverType> snowRemovers = new List<SnowRemoverType>();
                Random rnd = new Random();
                if (rnd.NextDouble() < 0.3)
                    snowRemovers.Add(SnowRemoverType.Rotor);
                else if (rnd.NextDouble() < 0.5)
                    snowRemovers.Add(SnowRemoverType.PlowBrush);
                else
                    snowRemovers.Add(SnowRemoverType.Shovel);
                if (rnd.NextDouble() > 0.4)
                    snowRemovers.Add(SnowRemoverType.Cleaver);
                else
                    snowRemovers.Add(SnowRemoverType.AntiIceDistributor);
                obj = new SnowRemover(lastClickPos, snowRemovers.ToArray());
            }
            if (obj == null)
                return;
            AgentStation? near_ags = (AgentStation)Director.Map.Stations.Where(p => p is AgentStation).MinBy(p => (p.Position - lastClickPos).Length);
            if (near_ags == null)
                return;
            Agent a = obj as Agent;
            a.Home = near_ags;
            break;
            case "31":
            obj = new Stations.GasStation(lastClickPos);
            break;
            case "32":
            obj = new Stations.AgentStation(lastClickPos);
            break;
            case "33":
            obj = new Stations.Meteostation(lastClickPos);
            break;
            case "34":
            obj = new Stations.AntiIceStation(lastClickPos);
            break;
            case "35":
            obj = new Stations.CollectingStation(lastClickPos);
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
            if (prevLastClickPos.X == prevLastClickPos.Y && prevLastClickPos.X == 0) {
                prevLastClickPos = new Point(lastClickPos.X, lastClickPos.Y);
                var r = Director?.Map.Roads.MinBy(p => p.DistanceToRoad(prevLastClickPos));
                if (r?.DistanceToRoad(prevLastClickPos) < 25) {
                    Vector v = (Vector)r;
                    (v.X, v.Y) = (v.Y, v.X);
                    v.Normalize(); v *= r.DistanceToRoad(prevLastClickPos);
                    if (r.DistanceToRoad(v + prevLastClickPos) > r.DistanceToRoad(-v + prevLastClickPos))
                        v.Negate();
                    prevLastClickPos += v;
                    Vector v1 = (prevLastClickPos - r.Position), v2 = (prevLastClickPos - r.EndPosition);
                    if (v1.Length < 20)
                        prevLastClickPos = r.Position;
                    else if (v2.Length < 20)
                        prevLastClickPos = r.EndPosition;
                }
                (prevLastClickPos.X, prevLastClickPos.Y) = (Math.Round(prevLastClickPos.X), Math.Round(prevLastClickPos.Y));
                Rectangle el = new Rectangle();
                el.Width = el.Height = 10;
                el.Margin = new Thickness(prevLastClickPos.X, prevLastClickPos.Y, 0, 0);
                el.Fill = new SolidColorBrush(Colors.DarkSlateGray);
                mapCanvas.Children.Add(el);

                (mapCanvas.ContextMenu.Items[0] as MenuItem).IsEnabled = false;
                (mapCanvas.ContextMenu.Items[1] as MenuItem).IsEnabled = false;
                (mapCanvas.ContextMenu.Items[3] as MenuItem).IsEnabled = true;
                (mapCanvas.ContextMenu.Items[3] as MenuItem).Tag = button.Tag;
            }
            break;
            default:
            break;
        }
        if (obj == null) return;
        Director.Add(obj);
        mapCanvas.Children.Add(obj.Build());
        if (obj is IPlaceableWithArea obja)
            mapCanvas.Children.Add(obja.BuildArea());
        if (obj is Transporter t)
            mapCanvas.Children.Add(t.BuildTrajectory());

        lastClickPos = new Point(0, 0);
    }
    private void startButton_Click(object sender, RoutedEventArgs e) {
        if (startB.Content.ToString() == "▶️") {
            speedSlider.Value = lastSpeed;
        } else {
            lastSpeed = speedSlider.Value;
            speedSlider.Value = 0;
        }
    }
    private void stopB_Click(object sender, RoutedEventArgs e) {
        Stop();
    }
    private void FileMenuItem_Click(object sender, RoutedEventArgs e) {
        var button = sender as MenuItem;
        Microsoft.Win32.FileDialog fd = null;
        object serialized_object;
        bool? action_is_open = button.Tag.ToString().Contains("open") ? true
            : button.Tag.ToString().Contains("save") ? false
            : null;
        if (!action_is_open.HasValue) {
            Director = new Director(mapCanvas.RenderSize);
            return;
        }
        fd = action_is_open.Value ? new Microsoft.Win32.OpenFileDialog() : new Microsoft.Win32.SaveFileDialog();
        if (button.Tag.ToString().Contains("model")) {
            fd.Filter = "Файлы модели|*.xml|Все файлы|*.*";
            serialized_object = Director;
        } else if (button.Tag.ToString().Contains("map")) {
            fd.Filter = "Файлы разметки|*.xml|Все файлы|*.*";
            serialized_object = Director.Map;
        } else return;
        if (fd.ShowDialog() != true || fd.FileName == "")
            return;
        if (serialized_object == Director) {
            if (action_is_open == true) {
                Director = Director.Deserialize(fd.FileName);
                if (tester.Models.Any() && tester.Models[0] is CopyModel cm && cm.Unpack() != Director)
                    tester.Models[0] = new CopyModel(Director) { Path = fd.FileName };
                tester.AttemptsN = tester.Models[0].MaxAttempts;
                DrawPlaceableObjects();
            } else
                Director?.Serialize(fd.FileName);
            return;
        }
        if (action_is_open == true && serialized_object == Director.Map)
            Director.MapPath = fd.FileName;
        else if (action_is_open == false)
            Director.Map.Save(fd.FileName);
        DrawPlaceableObjects();
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

    private void meteoCB_Checked(object sender, RoutedEventArgs e) {
        if (Director is not null)
            Director.EnableMeteo = meteoCB.IsChecked.Value;
    }

    private void speedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
        const double maxInterval = 500000;
        if (e.NewValue > 0) {
            refreshTimer.Interval = new TimeSpan((int)Math.Round(maxInterval / e.NewValue));
            Pause = false;
        } else {
            Pause = true;
        }
    }

    private void testsB_Click(object sender, RoutedEventArgs e) {
        Analyzing.Tests.TestsWindow window = new Analyzing.Tests.TestsWindow();
        window.ShowDialog();
        tester.LoadModels();
        if (File.Exists(tester.Models[0].Path)) {
            tester.LoadModel(tester.Models[0].Path);
            Director = tester.ActiveDirector;
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
        Director.Dispose();
    }

    public void Refresh() {
        if (Director != null) {
            double quality = Math.Round(Director.Distributor.DistributionQualifyReadings
                .Sum(p => p.Value.TakedLevel - (p.Value.TakedTarget as Snowdrift).Level));

            localTimeL.Content = $"Местное время: {Director.Time.ToShortTimeString()}  {Director.Time.ToLongDateString()}";
            systemQualityL.Content = $"Q = {quality}        Эпоха: {Director.Recorder.Epoch}";
            if (Director.Recorder.SystemQuality.Any()) {
                double best_qualitity = Director.Recorder.SystemQuality.Max();
                int best_quality_epoch = Director.Recorder.SystemQuality.IndexOf(best_qualitity) + 1;

                bestQualityL.Content = $"Q_best: {Math.Round(best_qualitity, 4)} (эп. {best_quality_epoch})" +
                    $" Q_{Director.Recorder.SystemQuality.Count}: {Director.Recorder.SystemQuality.Last()}";
            }
            if (drawCB.IsChecked == true) {
                if (Director.Agents.Any())
                    wayTimeCountL.Content = $"Время в пути: {Director.WayIterations} it";
            }
        }
    }
}
