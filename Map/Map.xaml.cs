#define ALWAYS
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

using SRDS.Analyzing;
using SRDS.Agents;
using SRDS.Agents.Drones;
using SRDS.Environment;

namespace SRDS.Map {
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
					director.Meteo.PropertyChanged += RefreshMeteo;
                    director.Map.Borders = mapCanvas.RenderSize;
				}
            }
        }
        TimeSpan tempTime = TimeSpan.Zero;
        DateTime startTime = DateTime.MinValue, pauseTime, d_time = new DateTime(0);
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
                Interval = new TimeSpan(0,0,0,0,100)
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
                try {
                    Director = tester.LoadModel(tester.Models[0].Path);
					attemptsCountL.Content = $"Измерений осталось: {tester.AttemptsN}\n" +
						$"Транспортеров: {Director.Agents.Length}\n" +
						$"Моделей: {tester.Models.Length}";
				} catch (FileNotFoundException ex) {
					mapCanvas.Children.Clear();
					Director = new Director(mapCanvas.RenderSize);
				}
            }
        }
		#region Drawing
		private void RefreshMeteo(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
			for (int i = 0; i < mapCanvas.Children.Count; i++)
				if (mapCanvas.Children[i].Uid == "cloud")
					mapCanvas.Children.Remove(mapCanvas.Children[i]);
			foreach (SnowCloud o in Director.Meteo.Clouds)
				mapCanvas.Children.Add(o.Build());
		}
		private void DrawPlaceableObjects() {
            var objs = Director.AllObjectsOnMap.Concat(Director.Map.Roads).Concat(Director.Meteo.Clouds).ToArray();
			for (int i = 0; i < objs.Length; i++) {
                IPlaceable obj = objs[i];
                var UIObj = obj.Build();
                mapCanvas.Children.Add(UIObj);

                if (obj is IPlaceableWithArea obja)
					mapCanvas.Children.Add(obja.BuildArea());

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
                Director.DistributeTask();
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
                recorder.Dispose();
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
            if (testingCB.IsChecked == false && tester.Models?.Length < 1)
                Director = tester.ReloadModel();
            else
                Director = new Director(mapCanvas.RenderSize);
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
					Director = SRDS.Director.Deserialize(fd.FileName);
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
                localTimeL.Content = $"Местное время: {Director.Meteo.Time.ToShortTimeString()}";
                currentObjsCountL.Content = "Осталось целей: " +
                    (Director.Targets.Length - Director.CollectedTargets.Length).ToString();
                traversedWayL.Content = $"Пройденный путь: {Math.Round(Director.TraversedWaySum)} px";
                if (drawCB.IsChecked == true) {
                    thinkTimeCountL.Content = $"Сложность расчёта: {Director.ThinkingIterations} it";
                    if (Director.Agents.Any())
                        wayTimeCountL.Content = $"Время в пути: {Director.WayIterations} it";
                    allTimeCountL.Content = $"Время алгоритма: {Math.Round((DateTime.Now - startTime).TotalSeconds, 3)} s";
                }
            }
        }
    }
}
