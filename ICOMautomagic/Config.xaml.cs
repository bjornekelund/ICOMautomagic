using System.Windows;
using System.IO.Ports;
using System.Windows.Input;

namespace ICOMAutomagic
{
    public partial class Config : Window
    {
        readonly string[] ICOMradios = new string[] { "IC-7300", "IC-7600", "IC-7610", "IC-7700", "IC-7800", "IC-7850", "IC-7851" };

        readonly MainWindow mw;

        public Config(MainWindow mainform)
        {
            InitializeComponent();

            mw = mainform;

            string[] ports = SerialPort.GetPortNames();

            Top = mainform.Top + 10;
            Left = mainform.Left + 50;

            for (int i = 0; i < ICOMradios.Length; i++)
                radioModelCombobox.Items.Add(ICOMradios[i]);
            radioModelCombobox.SelectedItem = Properties.Settings.Default.RadioModel;

            foreach(string port in ports)
                comPortCombobox.Items.Add(port);
            comPortCombobox.SelectedItem = Properties.Settings.Default.COMport;

            for (int i = 1; i <= 3; i++)
                edgeSetCombobox.Items.Add(i.ToString("00"));
            edgeSetCombobox.SelectedItem = Properties.Settings.Default.EdgeSet.ToString("00");

            for (int i = 4800; i <= 19200; i *= 2)
                ComPortSpeedCombobox.Items.Add(i.ToString());
            ComPortSpeedCombobox.SelectedItem = Properties.Settings.Default.COMportSpeed.ToString();

            zoomWidthTB.Text = Properties.Settings.Default.ZoomWidth.ToString();
            broadcastUDPtextbox.Text = Properties.Settings.Default.UDPPort.ToString();
            onTopCheckbox.IsChecked = Properties.Settings.Default.AlwaysOnTop;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OKbutton_Action()
        {
            if (!int.TryParse(broadcastUDPtextbox.Text, out int udpPort))
                return;

            if (!int.TryParse(zoomWidthTB.Text, out int zoom))
                return;

            Properties.Settings.Default.COMport = comPortCombobox.Text == "" ? "COM5" : comPortCombobox.Text;
            Properties.Settings.Default.UDPPort = udpPort;
            Properties.Settings.Default.RadioModel = radioModelCombobox.Text;
            Properties.Settings.Default.ZoomWidth = zoom;
            Properties.Settings.Default.COMportSpeed = int.Parse(ComPortSpeedCombobox.Text);
            Properties.Settings.Default.EdgeSet = byte.Parse(edgeSetCombobox.Text);
            Properties.Settings.Default.AlwaysOnTop = (bool)onTopCheckbox.IsChecked;

            switch (radioModelCombobox.Text)
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
                    radioModelCombobox.Text = "IC-7300";
                    Properties.Settings.Default.CIVaddress = 0x94;
                    break;
            }

            mw.Topmost = Properties.Settings.Default.AlwaysOnTop;

            Properties.Settings.Default.Save();
        }

        private void OKbutton_Click(object sender, RoutedEventArgs e)
        {
            OKbutton_Action();
            Close();
        }

        private void UDPTextbox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                OKbutton_Action();
                Close();
            }
        }

        private void ZoomWidthTextbox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                OKbutton_Action();
                Close();
            }
        }
    }
}
