using System.Windows;
using System.IO.Ports;
using System.Windows.Input;

namespace ICOMautomagic
{
    public partial class Config : Window
    {
        readonly string[] ICOMradios = new string[] { "IC-7300", "IC-7600", "IC-7610", "IC-7700", "IC-7800", "IC-7850", "IC-7851" };

        public Config(MainWindow mw)
        {
            InitializeComponent();

            string[] ports = SerialPort.GetPortNames();

            Top = mw.Top + 10;
            Left = mw.Left + 50;

            for (int i = 0; i < ICOMradios.Length; i++)
                radioModelCB.Items.Add(ICOMradios[i]);
            radioModelCB.SelectedItem = Properties.Settings.Default.RadioModel;

            foreach(string port in ports)
                comPortCB.Items.Add(port);
            comPortCB.SelectedItem = Properties.Settings.Default.COMport;

            for (int i = 1; i <= 3; i++)
                edgeSetCB.Items.Add(i.ToString("00"));
            edgeSetCB.SelectedItem = Properties.Settings.Default.EdgeSet.ToString("00");

            for (int i = 4800; i <= 19200; i *= 2)
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

        private void OKbutton_Action()
        {
            if (!int.TryParse(n1mmUdpTB.Text, out int n1mmPort))
                return;

            if (!int.TryParse(dxlogUdpTB.Text, out int dxlogPort))
                return;

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
                default: // IC-7300 is default
                    radioModelCB.Text = "IC-7300";
                    Properties.Settings.Default.CIVaddress = 0x94;
                    break;
            }

            Properties.Settings.Default.N1MMPort = n1mmPort;
            Properties.Settings.Default.DXLogPort = dxlogPort;
            Properties.Settings.Default.RadioModel = radioModelCB.Text;
            Properties.Settings.Default.ZoomWidth = zoom;
            Properties.Settings.Default.COMport = comPortCB.Text;
            Properties.Settings.Default.COMportSpeed = int.Parse(ComPortSpeedCB.Text);
            Properties.Settings.Default.DXLogStation = stnameDxlogTB.Text;
            Properties.Settings.Default.EdgeSet = byte.Parse(edgeSetCB.Text);
            Properties.Settings.Default.Save();
        }

        private void OKbutton_Click(object sender, RoutedEventArgs e)
        {
            OKbutton_Action();
            Close();
        }

        private void stnameDxlogTB_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                OKbutton_Action();
                Close();
            }
        }

            private void dxlogUdpTB_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                OKbutton_Action();
                Close();
            }
        }

        private void n1mmUdpTB_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                OKbutton_Action();
                Close();
            }
        }

        private void zoomWidthTB_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                OKbutton_Action();
                Close();
            }
        }
    }
}
