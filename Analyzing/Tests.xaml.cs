using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Navigation;

namespace SRDS.Analyzing.Tests {
    /// <summary>
    /// Логика взаимодействия для Tests.xaml
    /// </summary>
    public partial class TestsWindow : Window {
        FileSystemWatcher fs;
        public TestsWindow() {
            InitializeComponent();
            DirectoryInfo[] testsDirectory = Directory.CreateDirectory(Paths.Default.Tests).GetDirectories();
            fs = new FileSystemWatcher(Path.Combine(Paths.Default.Tests, "Active"));
            fs.Changed += Fs_Changed;
            Fs_Changed(null, null);
        }

        private void Fs_Changed(object sender, FileSystemEventArgs e) {
            activeLB.Items.Clear();
            inactiveLB.Items.Clear();
            foreach (string f in Directory.GetFiles(Path.Combine(Paths.Default.Tests, "Active")))
                activeLB.Items.Add(f[(Array.LastIndexOf(f.ToCharArray(), '\\')+1)..f.Length]);
            foreach (string f in Directory.GetFiles(Path.Combine(Paths.Default.Tests, "Complete")))
                inactiveLB.Items.Add(f[(Array.LastIndexOf(f.ToCharArray(), '\\')+1)..f.Length]);
            foreach (string f in Directory.GetFiles(Path.Combine(Paths.Default.Tests, "Inactive")))
                inactiveLB.Items.Add(f[(Array.LastIndexOf(f.ToCharArray(), '\\')+1)..f.Length]);
        }

        private void ActivateB_Click(object sender, RoutedEventArgs e) {
            if (inactiveLB.SelectedItems.Count < 1) return;
            foreach (var f in inactiveLB.SelectedItems) {
                string testInactivePath = Path.Combine(Paths.Default.Tests, "Inactive", f.ToString());
                string testCompletePath = Path.Combine(Paths.Default.Tests, "Complete", f.ToString());
                Directory.Move(File.Exists(testInactivePath) ? testInactivePath : testCompletePath,
                    Path.Combine(Paths.Default.Tests, "Active", f.ToString()));
            }
            Fs_Changed(null,null);
        }

        private void DeactivateB_Click(object sender, RoutedEventArgs e) {
            if (activeLB.SelectedItems.Count < 1) return;
            foreach (var f in activeLB.SelectedItems) {
                Directory.Move(Path.Combine(Paths.Default.Tests, "Active", f.ToString()),
                Path.Combine(Paths.Default.Tests, "Inactive", f.ToString()));
            }
            Fs_Changed(null,null);
        }
    }
}
