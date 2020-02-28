using System.Windows;
using System.Windows.Media;

namespace ReplyVision
{
    /// <summary>
    /// Logica di interazione per DebugImageWindow.xaml
    /// </summary>
    public partial class DebugImageWindow : Window
    {
        public DebugImageWindow(ImageSource source)
        {
            InitializeComponent();
            DebugImg.Source = source;
        }
    }
}
