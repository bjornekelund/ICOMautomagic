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

        readonly string[] ICOMradios = new string[] { "IC-7300", "IC-7600", "IC-7610", "IC-7700", "IC-7800", "IC-7850", "IC-7851" };

        public Config(MainWindow mw)
        {
            InitializeComponent();
            Top = mw.Top + 10;
            Left = mw.Left + 50;

            mainwindow = mw;

            for (int i = 0; i < ICOMradios.Length; i++)
                radioModelCB.Items.Add(ICOMradios[i]);
            radioModelCB.SelectedItem = Properties.Settings.Default.RadioModel;

            for (int i = 1; i <= 30; i++)
                comPortCB.Items.Add("COM" + i.ToString());
            comPortCB.SelectedItem = Properties.Settings.Default.COMport;

            for (int i = 1; i <= 3; i++)
                edgeSetCB.Items.Add(i.ToString("00"));
            edgeSetCB.SelectedItem = Properties.Settings.Default.EdgeSet.ToString("00");

            for (int i = 4800; i <= 115200; i += i)
                ComPortSpeedCB.Items.Add(i.ToString());
            ComPortSpeedCB.SelectedItem = Properties.Settings.Default.COMportSpeed.ToString();

            zoomWidthTB.Text = Properties.Settings.Default.ZoomWidth.ToString();

            stnameDxlogTB.Text = Properties.Settings.Default.DXLogStation;

            dxlogUdpTB.Text = Properties.Settings.Default.DXLogPort.ToString();

            n1mmUdpTB.Text = Properties.Settings.Default.N1MMPort.ToString();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OKbutton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(n1mmUdpTB.Text, out int n1mmport))
                return;
            Properties.Settings.Default.N1MMPort = n1mmport;

            if (!int.TryParse(dxlogUdpTB.Text, out int dxlogport))
                return;
            Properties.Settings.Default.DXLogPort = dxlogport;

            if (!int.TryParse(zoomWidthTB.Text, out int zoom))
                return;

            switch (radioModelCB.Text)
            {
                case "IC-7600":
                    Properties.Settings.Default.CIVaddress = 0x7a;
                    break;
                case "IC-7610":
                    Properties.Settings.Default.CIVaddress = 0x98;
                    break;
                case "IC-7700":
                    Properties.Settings.Default.CIVaddress = 0x74;
                    break;
                case "IC-7800":
                    Properties.Settings.Default.CIVaddress = 0x6a;
                    break;
                case "IC-7850":
                case "IC-7851":
                    Properties.Settings.Default.CIVaddress = 0x8e;
                    break;
                default: // IC-7300
                    radioModelCB.Text = "IC-7300";
                    Properties.Settings.Default.CIVaddress = 0x94;
                    break;
            }

            Properties.Settings.Default.RadioModel = radioModelCB.Text;

            Properties.Settings.Default.ZoomWidth = zoom;

            Properties.Settings.Default.COMport = comPortCB.Text;

            Properties.Settings.Default.COMportSpeed = int.Parse(ComPortSpeedCB.Text);

            Properties.Settings.Default.DXLogStation = stnameDxlogTB.Text;

            Properties.Settings.Default.Save();

            Close();
        }

        private void DxLogRB_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void N1mmRB_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}
