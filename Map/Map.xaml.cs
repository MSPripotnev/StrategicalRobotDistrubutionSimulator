using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Serialization;
using System.Windows.Data;
using System.IO;
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
        TimeSpan tempTime = new TimeSpan(0);
        int iterations = 0;
        Task[] directorTasks = new Task[2];
        DispatcherTimer refreshTimer;
        public MapWPF() {
            InitializeComponent();
            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = new TimeSpan(0,0,0,0,100);
            refreshTimer.Tick += RefreshTimer_Tick;
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
        }
        private void DrawPlaceableObjects() {
            for (int i = 0; i < director.AllObjectsOnMap.Count; i++) {
                IPlaceable obj = director.AllObjectsOnMap[i];
                mapCanvas.Children.Add(obj.Build());
                if (obj is Transporter t) {
                    mapCanvas.Children.Add(t.BuildTrajectory());
                }
            }
        }

        DateTime startTime = DateTime.MinValue, pauseTime;
        Task mainTask;
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        private void startButton_Click(object sender, RoutedEventArgs e) {
            if (startB.Content.ToString() == "Запуск") {
                for (int i = 0; i < menu.Items.Count; i++)
                    (menu.Items[i] as UIElement).IsEnabled = false;
                tokenSource = new CancellationTokenSource();
                mainTask = new Task(() => {
                    director.DistributeTask();
                    while (true) {
                        director.Work();
                        Dispatcher.Invoke(new Action(() => {
                            pauseTime = DateTime.Now;
                            director.DistributeTask();
                            tempTime -= DateTime.Now - pauseTime;
                        }));
                        if (tokenSource.IsCancellationRequested) 
                            break;
                        //Refresh();
                    }
                }, tokenSource.Token, TaskCreationOptions.LongRunning);
                refreshTimer.Start();
                startB.Content = "Стоп";
                if (startTime == DateTime.MinValue) {
                    pauseTime = startTime;
                }
                else {
                    tempTime += pauseTime - startTime;
                }
                startTime = DateTime.Now;
                mainTask.Start();
            } else {
                pauseTime = DateTime.Now;
                refreshTimer.Stop();
                tokenSource.Cancel();
                startB.Content = "Запуск";
            }
        }

        Point newObjectPos = new (0, 0);
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.RightButton == MouseButtonState.Pressed) {
                var loc = (e.GetPosition(_window));
                newObjectPos = new Point(loc.X, loc.Y);
                //mainCMS.Show(loc.X, loc.Y);
            }
            else if (e.LeftButton == MouseButtonState.Pressed) {
                if (new_obstacle != null) {
                    mapCanvas.Children.Remove(new_obstacle);
                    PointCollection ps = new_obstacle.Points;
                    ps.Add(e.GetPosition(_window));
                    new_obstacle = new Polygon();
                    new_obstacle.Fill = new SolidColorBrush(Colors.DarkSlateGray);
                    new_obstacle.Points = ps;
                    mapCanvas.Children.Add(new_obstacle);
                }
            }
        }
        private Polygon? new_obstacle = null;
        private void MenuItem_Click(object sender, RoutedEventArgs e) {
            var button = sender as MenuItem;
            IPlaceable obj = null;
            switch (button.Tag) {
                case "0":
                    obj = new Target(newObjectPos);
                    break;
                case "1":
                    obj = new Transporter(newObjectPos);
                    break;
                case "3":
                    obj = new Base(newObjectPos);
                    break;
                case "4":
                    if (new_obstacle == null) {
                        new_obstacle = new Polygon();
                        new_obstacle.StrokeThickness = 2;
                        new_obstacle.Fill = new SolidColorBrush(Colors.DarkSlateGray);
                        new_obstacle.Points = new PointCollection(new Point[]{ newObjectPos });
                        mapCanvas.Children.Add(new_obstacle);
                        Ellipse el = new Ellipse();
                        el.Width = el.Height = 5;
                        el.Margin = new Thickness(newObjectPos.X, newObjectPos.Y, 0, 0);
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
                    obj = new Base(newObjectPos);
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
            
            newObjectPos = new Point(0, 0);
        }

        private void _window_Loaded(object sender, RoutedEventArgs e) {

        }

        private void FileMenuItem_Click(object sender, RoutedEventArgs e) {
            var button = sender as MenuItem;
            switch (button.Tag) {
                case "0":
                    
                    break;
                case "1": //открыть
                    Microsoft.Win32.OpenFileDialog oFD = new Microsoft.Win32.OpenFileDialog();
                    oFD.Filter = "XML-файлы|*.xml|Все файлы|*.*";
                    if (oFD.ShowDialog() == true)
                        using (FileStream fs = new FileStream(oFD.FileName, FileMode.Open)) {
                            XmlSerializer serializer = new XmlSerializer(typeof(Director));
                            director = serializer.Deserialize(fs) as Director;
                            director.Refresh(1.0F, mapCanvas.RenderSize);
                            if (director == null) return;
                            mapCanvas.Children.Clear();
                            DrawPlaceableObjects();
                            startB.IsEnabled = true;
                        }
                    break;
                case "2": //сохранить
                    Microsoft.Win32.SaveFileDialog sFD = new Microsoft.Win32.SaveFileDialog();
                    sFD.Filter = "XML-файлы|*.xml|Все файлы|*.*";
                    if (sFD.ShowDialog() == true)
                        using (FileStream fs = new FileStream(sFD.FileName, FileMode.Create)) {
                            XmlWriterSettings settings = new XmlWriterSettings();
                            settings.Indent = true;
                            settings.IndentChars = "\t";
                            XmlSerializer serializer = new XmlSerializer(typeof(Director));
                            serializer.Serialize(XmlWriter.Create(fs, settings), director);
                        }
                    break;
                default:
                    break;
            }
        }

        private void deleteObjectB_Click(object sender, RoutedEventArgs e) {
            IPlaceable obj = null;
            obj = director.AllObjectsOnMap.Where(
                    p => Analyzer.Distance(p.Position, newObjectPos) < 30)
                .MinBy(
                    p => Analyzer.Distance(p.Position, newObjectPos));
            if (obj == null) {
                for(int i = 0; i < director.Obstacles.Length; i++)
                    if (director.Obstacles[i].PointOnObstacle(newObjectPos))
                        obj = director.Obstacles[i];
            }
            if (obj == null) return;
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

        public void Refresh() {
            collectedObjsCountL.Content = $"Количество собранных целей: {director.CollectedTargets.Length}";
            currentObjsCountL.Content = "Количество оставшихся целей: " + 
                (director.Targets.Length - director.CollectedTargets.Length).ToString();
            thinkTimeCountL.Content = $"Время расчёта: {Math.Round(director.ThinkingTime.TotalSeconds, 2).ToString()} s";
            wayTimeCountL.Content = $"Время в пути: {Math.Round((DateTime.Now - startTime + tempTime).TotalSeconds, 2)} s";
            allTimeCountL.Content = $"Время алгоритма: {Math.Round((DateTime.Now - startTime + tempTime + director.ThinkingTime).TotalSeconds, 2)} s";
            iterationsCountL.Content = "Количество итераций: " + iterations.ToString();
        }
    }
}
