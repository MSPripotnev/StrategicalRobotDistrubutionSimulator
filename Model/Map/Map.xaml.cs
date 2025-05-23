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
using Direct.Executive;
using Direct.Tactical.Qualifiers;
using Direct.Strategical;
using Environment;
using Stations;
using Targets;
using PropertyTools.Wpf;
using System;
using SRDS.Direct.Tactical;

/// <summary>
/// Логика взаимодействия для Map.xaml
/// </summary>
public partial class MapWPF : Window {
    readonly Paths paths = new Paths();
    public MapWPF() {
        InitializeComponent();
        paths.Reload();

        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.LowQuality);
        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;

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
                    $"Агентов: {Director.Agents.Length}\n" +
                    $"Моделей: {tester.Models.Length}";
            } catch (FileNotFoundException) {
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
        if (Director?.Meteo != null && Director.Map.Borders.Width > 0) {
            if (e.PropertyName == nameof(Director.Meteo.CloudControl.Clouds)) {
                for (int i = 0; i < mapCanvas.Children.Count; i++) {
                    if (mapCanvas.Children[i].Uid != nameof(SnowCloud)) continue;
                    if (!Director.Meteo.CloudControl.CloudsUI.Contains(mapCanvas.Children[i])) {
                        mapCanvas.Children.Remove(mapCanvas.Children[i]);
                        i--;
                    }
                }
                foreach (var c in Director.Meteo.CloudControl.CloudsUI.Where(p => p is not null && !mapCanvas.Children.Contains(p)))
                    if (cloudsLevelCB.IsChecked == true)
                        mapCanvas.Children.Add(c);
            }
            if (intensityMapCB.IsChecked == true)
                DrawMeteoIntensityMap(true);
        }
        if (e.PropertyName == nameof(Director.Map))
            scaleL.Content = $"1 пкс = {Director?.Map.MapScale} м";
    }
    private void DrawMeteoIntensityMap(bool draw) {
        for (int i = 0; i < Director?.Meteo?.IntensityControl.IntensityMap?.Length; i++) {
            for (int j = 0; j < Director.Meteo.IntensityControl.IntensityMap?[0].Length; j++) {
                var b = Director.Meteo.IntensityControl.IntensityMap[i][j]?.UI;
                if (b is null) continue;
                if (draw && !mapCanvas.Children.Contains(b))
                    mapCanvas.Children.Add(b);
                else if (!draw && mapCanvas.Children.Contains(b))
                    mapCanvas.Children.Remove(b);
            }
        }
    }
    private void DrawPlaceableObjects() {
        var objs = Director?.AllObjectsOnMap.Concat(Director.Map.Roads).ToArray();
        if (Director?.Meteo != null && cloudsLevelCB.IsChecked == true)
            objs = objs?.Concat(Director.Meteo.CloudControl.Clouds).ToArray();
        for (int i = 0; i < objs?.Length; i++) {
            IPlaceable obj = objs[i];
            var UIObj = obj.Build();
            if (!mapCanvas.Children.Contains(UIObj))
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
    #endregion

    #region Main Cycle
    readonly DispatcherTimer refreshTimer;
    TimeSpan ts = new TimeSpan(0);
    DateTime d_time = new DateTime(0);
    private Director? director;
    public Director? Director {
        get => director;
        set {
            director = value;
            if (director != null) {
                mapCanvas.Children.Clear();
                if (drawCB.IsChecked == true)
                    DrawPlaceableObjects();
                trajectoryScaleTB.Text = Math.Round(director.PathScale, 3).ToString();
                startB.IsEnabled = true;
                director.PropertyChanged += RefreshMeteo;

                SizeChanged -= Window_SizeChanged;
                Width = Math.Max(800, 1.7 * director.Map.Borders.Width);
                Height = Math.Max(600, 1.1 * director.Map.Borders.Height);
                mapCanvas.Width = director.Map.Borders.Width;
                mapCanvas.Height = director.Map.Borders.Height;
                if (Width > 1800 && Height > 1000)
                    WindowState = WindowState.Maximized;
                else
                    WindowState = WindowState.Normal;
                SizeChanged += Window_SizeChanged;

                director.Seed = 0;
                if (!tester.Models.Any()) {
                    var vs = tester.Models.ToList();
                    vs.Add(new CopyModel(director));
                    tester.Models = vs.ToArray();
                }
                tester.ModelSwitched += director.Recorder.OnModelSwitched;

                scaleL.Content = $"1 пкс = {director.Map.MapScale} м";
                planView.ItemsSource = director.Scheduler.Actions;

                if (director.Meteo is not null) {
                    director.Meteo.PropertyChanged += RefreshMeteo;
                    director.Meteo.CloudControl.PropertyChanged += RefreshMeteo;
                    if (director?.Meteo?.IntensityControl?.IntensityMap?.Any() == true) {
                        for (int i = 0; i < director.Meteo.IntensityControl.IntensityMap.Length; i++)
                            for (int j = 0; j < director.Meteo.IntensityControl.IntensityMap[i].Length; j++)
                                if (director.Meteo.IntensityControl.IntensityMap[i][j] is IntensityCell cell)
                                    cell.PropertyChanged += RefreshMeteo;
                    }
                }
            }
        }
    }
#if PARALLEL
    Task mainTask;
    CancellationTokenSource tokenSource = new CancellationTokenSource();
#endif
    private TimeSpan GetSpeedTs(int stepValue) => stepValue switch {
        1 => new TimeSpan(0, 0, 1),
        2 => new TimeSpan(0, 0, 5),
        3 => new TimeSpan(0, 0, 10),
        4 => new TimeSpan(0, 0, 30),
        5 => new TimeSpan(0, 0, 60),
        _ => new TimeSpan(0, 0, 1),
    };
    private void RefreshTimer_Tick(object? sender, EventArgs e) {
        refreshTimer.Stop();
        if (d_time.Second == 0 || ts.TotalSeconds == 0)
            ts = GetSpeedTs((int)speedSlider.Value);
        d_time = d_time.AddSeconds(ts.TotalSeconds);
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
    }
    private void Work() {
        try {
            if (Director is null) return;
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
            Director?.Recorder.Dispose();
            Director?.Dispose();
        }
        Refresh();
    }
    #endregion

    #region Simulation Content Control
    private void NextModelB_Click(object sender, RoutedEventArgs e) {
        tester.NextModel();
    }
    private void FileMenuItem_Click(object sender, RoutedEventArgs e) {
        if (sender is not MenuItem button || button.Tag?.ToString() is not string tag || Director is null) return;
        Microsoft.Win32.FileDialog fd;
        object? serialized_object;
        bool? action_is_open = tag.Contains("open") ? true
            : tag.Contains("save") ? false
            : null;
        if (!action_is_open.HasValue) {
            Director = new Director(mapCanvas.RenderSize);
            return;
        }
        fd = action_is_open.Value ? new Microsoft.Win32.OpenFileDialog() : new Microsoft.Win32.SaveFileDialog();
        if (tag.Contains("test")) {
            fd.InitialDirectory = paths.Tests;
            if (!tester.Models.Any())
                return;
            IModel model;
            if (tag.Contains("copy")) {
                fd.Filter = "Файлы случайных тестов|*.cmt|Все файлы|*.*";
                model = new CopyModel(Director) {
                    Seed = new Random().Next(0, int.MaxValue),
                    MaxAttempts = 250,
                    AttemptTime = DateTime.MinValue.AddDays(1),
                    Name = Director.Map.Name,
                };
                if (model is not CopyModel cmodel) throw new InvalidDataException();
                if (tester.Models[0] is CopyModel cm)
                    (cmodel as CopyModel).ModelPath = cm.ModelPath;
            } else return;
            serialized_object = model;
        } else if (tag.Contains("model")) {
            fd.InitialDirectory = paths.Models;
            fd.Filter = "Файлы модели|*.xsmod|Все файлы|*.*";
            serialized_object = Director;
        } else if (tag.Contains("map")) {
            fd.InitialDirectory = paths.Maps;
            fd.Filter = "Файлы разметки|*.xsmap|Все файлы|*.*";
            serialized_object = Director.Map;
        } else return;
        if (fd.ShowDialog() != true || fd.FileName == "")
            return;

        if (serialized_object == Director) {
            if (action_is_open == true) {
                Director = Director.Deserialize(fd.FileName);
                if (Director is null) return;
                if (tester.Models.Any() && tester.Models[0] is CopyModel cm && cm.Unpack() != Director)
                    tester.Models[0] = new CopyModel(Director) {
                        ModelPath = fd.FileName,
                        Name = fd.SafeFileName.Replace(".xsmod", $"-{nameof(CopyModel)}")
                    };
                tester.AttemptsN = tester.Models[0].MaxAttempts;
                DrawPlaceableObjects();
            } else
                Director?.Serialize(fd.FileName);
            return;
        }
        if (action_is_open == false && serialized_object is IModel smodel) {
            smodel.Path = fd.FileName;
            smodel.Save(fd.FileName);
            return;
        }

        if (Director is null) return;

        if (action_is_open == true && serialized_object == Director.Map)
            Director.MapPath = fd.FileName;
        else if (action_is_open == false)
            Director.Map.Save(fd.FileName);
        DrawPlaceableObjects();
    }

    #region Edit Current Content
    Point lastClickPos = new(0, 0);
    Point prevLastClickPos = new(0, 0);
    private Polygon? new_obstacle = null;
    private void MapCanvas_MouseDown(object sender, MouseButtonEventArgs e) {
        if (e.MiddleButton == MouseButtonState.Pressed || onPan || onResize)
            return;
        Point clickPos = e.GetPosition(mapCanvas);
        //if (e.RightButton == MouseButtonState.Pressed) {
        lastClickPos = new Point(Math.Round(clickPos.X, 2),
                                 Math.Round(clickPos.Y, 2));
        //}
        if (Director is null) return;
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
                    UIElement? el = cr.Build();
                    if (el is null) continue;
                    if (mapCanvas.Children.Contains(el))
                        mapCanvas.Children.Remove(el);
                    mapCanvas.Children.Add(el);
                }
                mapCanvas.Children.Add(r.Build());

                if (!Keyboard.IsKeyDown(Key.LeftShift)) {
                    prevLastClickPos = new Point(0, 0);
                    (mapCanvas.ContextMenu.Items[0] as MenuItem ?? throw new Exception()).IsEnabled = true;
                    (mapCanvas.ContextMenu.Items[1] as MenuItem ?? throw new Exception()).IsEnabled = true;
                    (mapCanvas.ContextMenu.Items[3] as MenuItem ?? throw new Exception()).IsEnabled = false;
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
                if (FindObject(clickPos) is IPlaceable obj) {
                    propertyGrid.Visibility = Visibility.Visible;
                    planView.Visibility = Visibility.Collapsed;
                    propertyGrid.SetBinding(PropertyGrid.SelectedObjectProperty, new Binding(".") { Source = obj, Mode = BindingMode.TwoWay });
                } else {
                    propertyGrid.SelectedObject = null;
                    propertyGrid.SetBinding(PropertyGrid.SelectedObjectProperty, "");
                    propertyGrid.Visibility = Visibility.Collapsed;
                    planView.Visibility = Visibility.Visible;
                }
            }
        }
    }
    private void MenuAssignesItem_Click(object sender, RoutedEventArgs e) {
        if (sender is not MenuItem button || Director is null) return;
        if (propertyGrid.SelectedObject is not Agent agent) throw new AccessViolationException();
        switch (button.Tag) {
        case "ap": {
            agent.TargetPosition = lastClickPos;
            break;
        }
        case "ar": {
            if (FindObject(lastClickPos) is not Road road || agent is not SnowRemover remover) throw new AccessViolationException();
            var action = Planner.WorkOnRoad(remover, road, d_time, DateTime.MaxValue, 0.0, 0.0) ?? throw new InvalidOperationException();
            director?.Scheduler.Add(action);
            break;
        }
        case "af": {
            if (director is null || director.Map is null) return;
            var action = Planner.RefuelPlan(agent, director.Map, d_time) ?? throw new InvalidOperationException();
            director?.Scheduler.Add(action);
            break;
        }
        }
        if (button.Tag is SnowRemoverType device) {
            if (FindObject(lastClickPos) is not AgentStation station || agent is not SnowRemover remover) throw new AccessViolationException();
            var action = Planner.ChangeDevicePlan(remover, device, d_time);
            if (action is null) return;

            director?.Scheduler.Add(action);
        }
    }
    private void MenuItem_Click(object sender, RoutedEventArgs e) {
        if (sender is not MenuItem button || Director is null) return;
        IPlaceable? obj = null;
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
                List<SnowRemoveDevice> snowRemovers = new();
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
            AgentStation? near_ags = (AgentStation?)Director.Map.Stations.Where(p => p is AgentStation).MinBy(p => (p.Position - lastClickPos).Length);
            if (near_ags is null)
                return;
            if (obj is not Agent a) return;
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

                (mapCanvas.ContextMenu.Items[0] as MenuItem ?? throw new Exception()).IsEnabled = false;
                (mapCanvas.ContextMenu.Items[1] as MenuItem ?? throw new Exception()).IsEnabled = false;
                (mapCanvas.ContextMenu.Items[3] as MenuItem ?? throw new Exception()).IsEnabled = true;
                (mapCanvas.ContextMenu.Items[3] as MenuItem ?? throw new Exception()).Tag = button.Tag;
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

                (mapCanvas.ContextMenu.Items[0] as MenuItem ?? throw new Exception()).IsEnabled = false;
                (mapCanvas.ContextMenu.Items[1] as MenuItem ?? throw new Exception()).IsEnabled = false;
                (mapCanvas.ContextMenu.Items[3] as MenuItem ?? throw new Exception()).IsEnabled = true;
                (mapCanvas.ContextMenu.Items[3] as MenuItem ?? throw new Exception()).Tag = button.Tag;
            }
            break;
        default:
            break;
        }
        if (obj == null) return;
        Director?.Add(obj);
        mapCanvas.Children.Add(obj.Build());
        if (obj is IPlaceableWithArea obja)
            mapCanvas.Children.Add(obja.BuildArea());
        if (obj is Transporter t)
            mapCanvas.Children.Add(t.BuildTrajectory());

        lastClickPos = new Point(0, 0);
    }
    private void DeleteObjectB_Click(object sender, RoutedEventArgs e) {
        IPlaceable? obj;
        obj = FindObject(lastClickPos);

        if (obj == null) return;
        if (propertyGrid.SelectedObject == obj)
            propertyGrid.SelectedObject = null;
        Director?.Remove(obj);
        mapCanvas.Children.Clear();

        DrawPlaceableObjects();
    }
    private void UndoB_Click(object sender, RoutedEventArgs e) {
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
    private void FinishObjectB_Click(object sender, RoutedEventArgs e) {
        IPlaceable? obj = null;
        if (finishObjectB.Tag.ToString() == "4") {
            mapCanvas.Children.Remove(new_obstacle);
            mapCanvas.Children.Remove(mapCanvas.Children[^1]);
            if (new_obstacle is not null)
                obj = new Obstacle(new_obstacle.Points.ToArray());
            new_obstacle = null;
        }
        if (obj != null && Director is not null) {
            Director.Add(obj);
            mapCanvas.Children.Add(obj.Build());
        }
        (mapCanvas.ContextMenu.Items[0] as MenuItem ?? throw new Exception()).IsEnabled = true;
        (mapCanvas.ContextMenu.Items[1] as MenuItem ?? throw new Exception()).IsEnabled = true;
        (mapCanvas.ContextMenu.Items[3] as MenuItem ?? throw new Exception()).IsEnabled = false;
    }
    #endregion

    private void CheckBox_Checked(object sender, RoutedEventArgs e) {
        if (Director == null) return;

        if (sender == meteoCB) {
            Director.EnableMeteo = meteoCB.IsChecked ?? false;
            intensityMapCB.IsChecked = false;
            intensityMapCB.IsEnabled = meteoCB.IsChecked ?? false;
            return;
        }

        if (sender == cloudsLevelCB) {
            if (cloudsLevelCB.IsChecked == false)
                for (int i = 0; i < mapCanvas.Children.Count; i++) {
                    if (mapCanvas.Children[i].Uid != nameof(SnowCloud)) continue;
                    mapCanvas.Children.Remove(mapCanvas.Children[i]);
                    i--;
                }
            else RefreshMeteo(sender, new System.ComponentModel.PropertyChangedEventArgs(nameof(SnowCloud)));
        }

        if (sender == intensityMapCB) {
            DrawMeteoIntensityMap(intensityMapCB.IsChecked ?? false);
            return;
        }

        if (drawCB.IsChecked ?? false) {
            DrawPlaceableObjects();
        } else {
            mapCanvas.Children.Clear();
            systemQualityL.Content = "";
            bestQualityL.Content = "";
            fuelCountL.Content = "";
            allTimeCountL.Content = "";
        }
        meteoCB.IsEnabled = !(nextModelB.IsEnabled = testingCB.IsChecked == true);
    }
    private void TrajectoryScale_TextChanged(object sender, TextChangedEventArgs e) {
        if (Director != null && float.TryParse((sender as TextBox ?? throw new Exception()).Text.Replace('.', ','), out float scale))
            Director.PathScale = scale;
    }
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {
        if (Pause && Director != null)
            Director.Map.Borders = mapCanvas.RenderSize;
    }
    private void Window_MouseWheel(object sender, MouseWheelEventArgs e) {
        if (director is not null) {
            director.Map.MapScale += e.Delta / 12;
            if (director.Map.MapScale > 200) director.Map.MapScale = 200;
            else if (director.Map.MapScale < 1) director.Map.MapScale = 1;
            scaleL.Content = $"1 пкс = {director.Map.MapScale} м";
        } else scaleL.Content = "";
    }
    #endregion

    #region Simulation Flow Control
    TimeSpan tempTime = TimeSpan.Zero;
    DateTime startTime = DateTime.MinValue, pauseTime;
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
    private void Start() {
        for (int i = 0; i < menu.Items.Count; i++)
            (menu.Items[i] as UIElement ?? throw new Exception()).IsEnabled = false;
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
        if (planView.ItemsSource == null && Director is not null)
            planView.ItemsSource = Director.Scheduler.Actions;
        MouseWheel -= Window_MouseWheel;
#endif
    }
    private void Stop() {
#if PARALLEL
        tokenSource.Cancel();
#endif
        Director?.Recorder.SaveStrategy(System.IO.Path.GetFileName(tester.Models[0].Path) + ".xml");
        if (testingCB.IsChecked == false) {
            Director = tester.ReloadModel();
            meteoCB.IsEnabled = true;
        }
        speedSlider.Value = 0;
        RefreshTime();
        Refresh();
        for (int i = 0; i < menu.Items.Count; i++)
            if (menu.Items[i] is UIElement el)
                el.IsEnabled = true;
        stopB.IsEnabled = false;
        stepB.IsEnabled = false;
        propertyGrid.SelectedObject = null;
        MouseWheel += Window_MouseWheel;
        planView.ItemsSource = null;
    }
    private void StartButton_Click(object sender, RoutedEventArgs e) {
        if (startB.Content.ToString() == "▶️") {
            speedSlider.Value = lastSpeed;
        } else {
            lastSpeed = speedSlider.Value;
            speedSlider.Value = 0;
        }
    }
    private void StopB_Click(object sender, RoutedEventArgs e) {
        Stop();
    }
    private void StepB_Click(object sender, RoutedEventArgs e) {
        refreshTimer.Stop();
        d_time = d_time.AddSeconds(ts.TotalSeconds);
        var dt = DateTime.Now;
        Work();
        var wts = DateTime.Now - dt;
        tempTime -= DateTime.Now - pauseTime; Refresh();
        tempTime += DateTime.Now - pauseTime;
        realWayTime += wts;
    }
    double lastSpeed = 1;
    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
        const int maxInterval = 20;
        if (e.NewValue > 0) {
            refreshTimer.Interval = new TimeSpan(0, 0, 0, 0, maxInterval);
            Pause = false;
        } else {
            Pause = true;
        }
    }
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
        if (e.Key == Key.Space)
            StartButton_Click(sender, new RoutedEventArgs());
        else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad5)
            speedSlider.Value = e.Key - Key.NumPad1 + 1;
    }
    #endregion

    #region Testing Control
    readonly Tester tester = new Tester();
    private void TestsB_Click(object sender, RoutedEventArgs e) {
        Analyzing.Tests.TestsWindow window = new Analyzing.Tests.TestsWindow();
        window.ShowDialog();
        tester.LoadModels();
        if (tester.Models.Any() && tester.Models[0].Path is not null && File.Exists(tester.Models[0].Path)) {
            tester.LoadModel(tester.Models[0].Path ?? "");
            Director = tester.ActiveDirector;
        }
    }
    private void OnAttemptStarted(object? sender, EventArgs e) {
        Director = tester.ReloadModel();

        attemptsCountL.Content = $"Измерений осталось: {tester.AttemptsN}\n" +
            $"Транспортеров: {Director.Agents.Length}\n" +
            $"Моделей: {tester.Models.Length}";
        trajectoryScaleTB.Text = Math.Round(Director.PathScale, 3).ToString();

        StartButton_Click(sender ?? this, new RoutedEventArgs());
    }
    private void OnAttemptCompleted(object? sender, EventArgs e) {
        realWorkTime += (DateTime.Now - startTime);
        if (Director is not null) {
            Director.Recorder.SaveResults(Director, tester.Models[0].Name, realWorkTime, ref iterations);
            Director.Learning.Select(Director.Recorder.AllEpochsTaskQualifyReadings, Director.Recorder.SystemQualifyReadings);
            if (Director.Distributor.Qualifier is FuzzyQualifier fq)
                Director.Learning.Mutate(ref fq.Net);
            Director.Distributor.DistributionQualifyReadings = new();
        }
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
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
        Director?.Dispose();
    }
    #endregion

    #region Simulation Info
    private double iterations = 0;
    TimeSpan realWorkTime = TimeSpan.Zero, realWayTime = TimeSpan.Zero;
    private void RefreshTime() {
        d_time = new DateTime(0);
        tempTime = TimeSpan.Zero;
        realWorkTime = TimeSpan.Zero;
        realWayTime = TimeSpan.Zero;
        pauseTime = DateTime.MinValue;
        startTime = DateTime.Now;
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
    private void MapCanvas_MouseMove(object sender, MouseEventArgs e) {
        var p = e.GetPosition(mapCanvas);
        if (mapCanvas.ToolTip is ToolTip t) {
            t.IsOpen = false;
            int x = (int)Math.Round(p.X), y = (int)Math.Round(p.Y);
            if (x < 0 || y < 0) return;
            t.Content = $"({x}; {y})";
            if (Director?.Meteo?.IntensityControl.IntensityMap?.Length > x / IntensityControl.IntensityMapScale && Director.Meteo.IntensityControl.IntensityMap[0].Length > y / IntensityControl.IntensityMapScale &&
                    Director.Meteo.IntensityControl.IntensityMap[x / IntensityControl.IntensityMapScale][y / IntensityControl.IntensityMapScale] is IntensityCell cell)
                t.Content += $"\nintensity: {Math.Round(cell.Snow, 4)}\n" +
                             $"icy: {Math.Round(cell.IcyPercent, 2)}%\n" +
                             $"deicing: {Math.Round(cell.Deicing, 2)}";
            t.IsOpen = true;
        } else {
            mapCanvas.ToolTip = new ToolTip();
        }
    }

    private void MapCanvas_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
        if (propertyGrid.SelectedObject is Agent) {
            if (FindObject(lastClickPos) is Road)
                assignB.Visibility = Visibility.Visible;
            else
                assignB.Visibility = Visibility.Collapsed;
            goToB.Visibility = Visibility.Visible;
            var obj = FindObject(lastClickPos);
            if (obj is AgentStation or GasStation or AntiIceStation) {
                refuelB.Visibility = Visibility.Visible;
                if (obj is AgentStation) {
                    changeDeviceB.Visibility = Visibility.Visible;
                    changeDeviceB.Items.Clear();
                    var ds = typeof(SnowRemoverType).GetEnumValues();
                    for (int i = 0; i < ds.Length; i++) {
                        var b = new MenuItem() {
                            Tag = ds.GetValue(i),
                            Name = $"device{typeof(SnowRemoverType).GetEnumName(i)}B",
                            Header = typeof(SnowRemoverType).GetEnumName(i)
                        };
                        b.Click += MenuAssignesItem_Click;
                        changeDeviceB.Items.Add(b);
                    }
                } else {
                    changeDeviceB.Visibility = Visibility.Collapsed;
                }
            } else {
                changeDeviceB.Visibility = Visibility.Collapsed;
                refuelB.Visibility = Visibility.Visible;
            }

            addObjectB.Visibility = Visibility.Collapsed;
            deleteObjectB.Visibility = Visibility.Collapsed;
            undoB.Visibility = Visibility.Collapsed;
            finishObjectB.Visibility = Visibility.Collapsed;
        } else {
            assignB.Visibility = Visibility.Collapsed;
            goToB.Visibility = Visibility.Collapsed;
            changeDeviceB.Visibility = Visibility.Collapsed;
            refuelB.Visibility = Visibility.Collapsed;

            addObjectB.Visibility = Visibility.Visible;
            deleteObjectB.Visibility = Visibility.Visible;
            undoB.Visibility = Visibility.Visible;
            finishObjectB.Visibility = Visibility.Visible;
        }
    }

    private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) {
        if (!Pause && Director is not null)
            Director.Map.Borders = mapCanvas.RenderSize;
    }

    private void GridSplitter_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) {
        if (!Pause && Director is not null && Director.Map.Borders.Width > 0 && Director.Map.Borders.Height > 0)
            mapCanvas.RenderSize = new(Director.Map.Borders.Width + e.HorizontalChange, Director.Map.Borders.Height + e.VerticalChange);
    }

    bool onPan = false, onResize = false;
    private void Window_MouseMove(object sender, MouseEventArgs e) {
        if (e.MiddleButton == MouseButtonState.Pressed) {
            onPan = true;
            Cursor = Cursors.SizeAll;
            lastClickPos = e.GetPosition(this);
            var diffPos = prevLastClickPos - lastClickPos;
            if (diffPos.X > 0 && mapCanvas.Margin.Left < -mapCanvas.Width / 2 || diffPos.X < 0 && mapCanvas.Margin.Left > mapCanvas.Width / 2)
                diffPos.X = 0;
            if (diffPos.Y > 0 && mapCanvas.Margin.Top < -mapCanvas.Height / 2 || diffPos.Y < 0 && mapCanvas.Margin.Top > mapCanvas.Height / 2)
                diffPos.Y = 0;
            if (prevLastClickPos.X != 0 && prevLastClickPos.Y != 0)
                mapCanvas.Margin = new Thickness(mapCanvas.Margin.Left - diffPos.X, mapCanvas.Margin.Top - diffPos.Y, 0, 0);
            prevLastClickPos = lastClickPos;
        } else if (onPan) {
            Cursor = default;
            prevLastClickPos = new Point(0, 0);
            onPan = false;
        } 
        if ((e.GetPosition(mapCanvas) - new Point(mapCanvas.Width, mapCanvas.Height)).Length < 5 || onResize) {
            Cursor = Cursors.SizeNWSE;
            lastClickPos = e.GetPosition(this);
            if (e.LeftButton == MouseButtonState.Pressed) {
                onResize = true;
                var diffPos = prevLastClickPos - lastClickPos;
                if (prevLastClickPos.X != 0 && prevLastClickPos.Y != 0) {
                    mapCanvas.Width -= diffPos.X;
                    mapCanvas.Height -= diffPos.Y;
                    if (Pause && Director != null)
                        Director.Map.Borders = mapCanvas.RenderSize;
                }
                prevLastClickPos = lastClickPos;
            } else {
                onResize = false;
                prevLastClickPos = new Point(0, 0);
            }
        } else {
            Cursor = default;
            onResize = false;
        }
    }

    private void MapCanvas_MouseLeave(object sender, MouseEventArgs e) {
        if (mapCanvas.ToolTip is ToolTip t)
            t.IsOpen = false;
    }

    public void Refresh() {
        if (Director != null) {
            double snow = Math.Round(Director.Map.Roads.Sum(p => p.Snowness) * 1000),
                   icy = Math.Round(Director.Map.Roads.Max(p => p.IcyPercent));
            localTimeL.Content = $"Местное время: {Director.Time.ToLongTimeString()} {Director.Time.ToLongDateString()}";
            systemQualityL.Content = $"Заснеженность дорог: {snow} мм\t\nУровень гололёда: {icy}%";
            if (Director.Recorder.SystemQualifyReadings.Any()) {
                double best_qualitity = Director.Recorder.SystemQualifyReadings.Max();
                int best_quality_epoch = Director.Recorder.SystemQualifyReadings.IndexOf(best_qualitity) + 1;

                bestQualityL.Content = $"Q_best: {Math.Round(best_qualitity, 4)} (эп. {best_quality_epoch})" +
                    $" Q_{Director.Recorder.SystemQualifyReadings.Count}: {Director.Recorder.SystemQualifyReadings.Last()}";
            }
            if (drawCB.IsChecked == true) {
                if (Director.Agents.Any())
                    fuelCountL.Content = $"\nРасход топлива = {Math.Round(Director.Agents.Sum(p => p.FuelConsumption))} л\n" +
                                         $"Расход реагента: {Math.Round(Director.Agents.OfType<SnowRemover>().Sum(p => p.DeicingConsumption) / 1000.0, 2)} т";
            }
        }
    }
    #endregion
}
