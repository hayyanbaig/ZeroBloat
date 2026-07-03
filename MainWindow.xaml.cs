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
            var tweak = new ClassicContextMenuTweak();
            var preview = tweak.PreviewChange();
            MessageBox.Show($"PREVIEW\n{preview.TargetPath}\n{preview.OldValue} → {preview.NewValue}");

            var applyResult = tweak.Apply();
            MessageBox.Show($"APPLY\n{applyResult.Message}");

            var verifyResult = tweak.Verify();
            MessageBox.Show($"VERIFY\n{verifyResult.Message}");


            var tweak2 = new DisableSysMainTweak();
            var preview2 = tweak2.PreviewChange();
            MessageBox.Show($"PREVIEW\n{preview2.TargetPath}\n{preview2.OldValue} → {preview2.NewValue}");

            var applyResult2 = tweak2.Apply();
            MessageBox.Show($"APPLY\n{applyResult2.Message}");

            var verifyResult2 = tweak2.Verify();
            MessageBox.Show($"VERIFY\n{verifyResult2.Message}");


            var revertResult = tweak.Revert();
            MessageBox.Show($"REVERT\n{revertResult.Message}");

            var revertResult2 = tweak2.Revert();
            MessageBox.Show($"REVERT\n{revertResult2.Message}");

        }
    }
}