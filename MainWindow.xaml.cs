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
            var manifest = ManifestLoader.Load();
            var tweaks = TweakFactory.BuildAll(manifest);

            var sysMainTweak = tweaks.First(t => t.Id == "disable_sysmain");

            var preview = sysMainTweak.PreviewChange();
            MessageBox.Show($"BUILT FROM MANIFEST\n{sysMainTweak.DisplayName}\n{preview.OldValue} → {preview.NewValue}");

            var applyResult = sysMainTweak.Apply();
            MessageBox.Show($"APPLY\n{applyResult.Message}");

            var revertResult = sysMainTweak.Revert();
            MessageBox.Show($"REVERT\n{revertResult.Message}");
        }
    }
}