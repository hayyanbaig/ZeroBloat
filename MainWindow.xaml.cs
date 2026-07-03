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

            string[] idsToTest = new[]
            {
                "classic_context_menu",
                "disable_sysmain",
                "disable_copilot",
                "local_account_delink",
                "disable_recall",
                "kill_telemetry",
                "stop_activity_tracking",
                "disable_advertising_id",
                "disable_location_tracking",
                "disable_clipboard_cloud_sync",
                "disable_wifi_sense"
            };

            foreach (var id in idsToTest)
            {
                var tweak = tweaks.First(t => t.Id == id);

                var compat = tweak.CheckCompatibility();
                MessageBox.Show($"COMPATIBILITY\n{tweak.DisplayName}: {compat}");
                if (compat.ToString() != "Supported")
                    continue;

                var preview = tweak.PreviewChange();
                MessageBox.Show($"PREVIEW - {tweak.DisplayName}\n{preview.TargetPath}\n{preview.OldValue} → {preview.NewValue}");

                var applyResult = tweak.Apply();
                MessageBox.Show($"APPLY - {tweak.DisplayName}\n{applyResult.Message}");

                var verifyResult = tweak.Verify();
                MessageBox.Show($"VERIFY - {tweak.DisplayName}\n{verifyResult.Message}");

                var revertResult = tweak.Revert();
                MessageBox.Show($"REVERT - {tweak.DisplayName}\n{revertResult.Message}");
            }
        }
    }
}