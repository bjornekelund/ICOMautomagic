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

namespace ICOMautomagic
{
    public partial class Config : Window
    {
        MainWindow mainwindow;

        public Config(MainWindow mw)
        {
            InitializeComponent();
            Top = mw.Top + 10;
            Left = mw.Left + 50;

            mainwindow = mw;

            //modelComboBox.Items.Add("600S");
            //modelComboBox.Items.Add("700S");
            //modelComboBox.Items.Add("1200S");
            //modelComboBox.SelectedItem = model;

            //for (int i = 1; i <= 30; i++)
            //    portComboBox.Items.Add("COM" + i.ToString());
            //portComboBox.SelectedItem = port;

        }
        //private void CancelButton_Click(object sender, RoutedEventArgs e)
        //{
        //    Close();
        //}

        //private void OkButton_Click(object sender, RoutedEventArgs e)
        //{
        //    mainwindow.Configuration(portComboBox.Text, modelComboBox.Text);
        //    Close();
        //}

    }
}
