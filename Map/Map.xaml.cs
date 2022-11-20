using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TacticalAgro.Map {
    /// <summary>
    /// Логика взаимодействия для Map.xaml
    /// </summary>
    public partial class MapWPF : Window {
        Director director;
        double workTime = 0;
        int iterations = 0;
        Task[] directorTasks = new Task[2];
        DispatcherTimer refreshTimer;
        public MapWPF() {
            InitializeComponent();
            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = new TimeSpan(0,0,0,0,100);
            refreshTimer.Tick += RefreshTimer_Tick;
            director = new Director(mapCanvas.DesiredSize);
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e) {             
            //workTime += refreshTimer.Interval.TotalSeconds;
            //iterations++;
            //director.Work();
            //director.DistributeTask();
            //Task.Run(() => {
            //    director.DistributeTask();
            //});
            if (director.checkMission()) {
                //Task.WaitAll(directorTasks);
                director.Work();
                refreshTimer.Stop();
                startB.Content = "Запуск";
                //return;
            }
            director.Work();
            director.DistributeTask();
            Refresh();
            //Render();
            //mapCanvas.Invalidate();
        }
        private void DrawObstacles() {
            DrawingVisual drawingVisual = new DrawingVisual();
            DrawingContext drawingContext = drawingVisual.RenderOpen();
            Brush brush = new SolidColorBrush(Colors.DarkSlateGray);
            for (int i = 0; i < director.Obstacles.Count; i++) {
                Polygon polygon = new Polygon();
                polygon.Points = new PointCollection(director.Obstacles[i].Borders);
                polygon.Fill = new SolidColorBrush(Colors.DarkSlateGray);
                polygon.Uid = "obstacle_" + i.ToString();
                mapCanvas.Children.Add(polygon);
            }
        }
        private void DrawPlaceableObjects() {
            for (int i = 0; i < director.AllObjectsOnMap.Count; i++) {
                IPlaceable obj = director.AllObjectsOnMap[i];
                Brush brush = new SolidColorBrush(obj.Color);
                Pen pen = new Pen(brush, 5);
                mapCanvas.Children.Add(obj.Build());
                if (obj is Transporter t) {
                    mapCanvas.Children.Add(t.BuildTrajectory());
                }
            }
        }

        DateTime startTime;
        Task mainTask;
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        private void startButton_Click(object sender, RoutedEventArgs e) {
            if (startB.Content.ToString() == "Запуск") {
                tokenSource = new CancellationTokenSource();
                mainTask = new Task(() => {
                    director.DistributeTask();
                    while (true) {
                        director.Work();
                        director.DistributeTask();
                        if (tokenSource.IsCancellationRequested) 
                            break;
                        //Refresh();
                    }
                }, tokenSource.Token, TaskCreationOptions.LongRunning);
                refreshTimer.Start();
                startB.Content = "Стоп";
                startTime = DateTime.Now;
                //mainTask.Start();
            } else {
                //refreshTimer.Stop();
                tokenSource.Cancel();
                startB.Content = "Запуск";
            }
        }

        (float, float) newObjectPos = (0, 0);
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.RightButton == MouseButtonState.Released) {
                var loc = (mapCanvas.PointToScreen(e.GetPosition(_window)));
                newObjectPos = ((float)loc.X, (float)loc.Y);
                //mainCMS.Show(loc.X, loc.Y);
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e) {
            var button = sender as Button;
            switch (button.Tag) {
                case "0":
                    Target obj = new Target(new Point(newObjectPos.Item1, newObjectPos.Item2), Colors.Green);
                    director.Add(obj);
                    break;
                case "1":
                    Transporter r = new Transporter(new Point(newObjectPos.Item1, newObjectPos.Item2));
                    director.Add(r);
                    break;
                case "2":
                //Target @base = new Target(newObjectPos.Item1, newObjectPos.Item2, Color.Blue);
                //director.Add(@base);
                //break;
                default:
                    break;
            }
            //mapPanel.Invalidate();
            newObjectPos = (0, 0);
        }

        private void _window_Loaded(object sender, RoutedEventArgs e) {
            director.Refresh(1.0F, mapCanvas.RenderSize);
            DrawObstacles();
            DrawPlaceableObjects();
            //Render();
        }

        public void Refresh() {
            collectedObjsCountL.Content = "Количество собранных целей: " + director.CollectedTargets.Length.ToString();
            currentObjsCountL.Content = "Количество оставшихся целей: " + 
                (director.Targets.Length - director.CollectedTargets.Length).ToString();
            timeCountL.Content = "Прошедшее время: " + Math.Round((DateTime.Now - startTime).TotalSeconds, 2) + " s";
            iterationsCountL.Content = "Количество итераций: " + iterations.ToString();
        }
    }
}
