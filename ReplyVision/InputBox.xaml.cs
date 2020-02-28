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

namespace ReplyVision
{
    /// <summary>
    /// Logica di interazione per InputBox.xaml
    /// </summary>
    public partial class InputBox : Window
    {
        public string Value { get; private set; }
        public bool Result { get; private set; }

        public InputBox(string caption, string label = "")
        {
            InitializeComponent();
            Title = caption;
            LblLabel.Content = label;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Value = TbValue.Text;
            Result = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public bool ShowDialog(out string value)
        {
            ShowDialog();
            value = Value;
            return Result;
        }
    }
}
