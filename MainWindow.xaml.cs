using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ZeroBloat.Services.Manifest;
using ZeroBloat.Tweaks;
using ZeroBloat.Tweaks.Modules;

namespace ZeroBloat
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var manifest = ManifestLoader.Load(forceReload: true);
            var tweaks = TweakFactory.BuildAll(manifest);

            MessageBox.Show($"Loaded {tweaks.Count} tweaks:\n" +
                string.Join("\n", tweaks.Select(t => $"- {t.DisplayName} [{t.CheckCompatibility()}]")));
        }
    }
}