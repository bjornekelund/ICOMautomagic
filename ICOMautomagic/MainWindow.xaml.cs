// Demo broadcast listener for N1MM Logger+
// Receives UDP broadcasts on listenPort, parses XML into an object and prints info
// Intended as starting point for development of more applications such as 
// big screen score board, out-of-band alarm, etc.
// By Björn Ekelund SM7IUN sm7iun@ssa.se 2019-02-05

using System;
using System.Linq;
using System.Net.Sockets;
using System.Xml.Linq;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using System.IO.Ports;

namespace ICOMautomagic
{
    // Definition of N1MM XML datagrams based on
    // http://n1mm.hamdocs.com/tiki-index.php?page=UDP+Broadcasts

    [XmlRoot(ElementName = "RadioInfo")]
    public class RadioInfo
    {
        [XmlElement(ElementName = "StationName")]
        public string StationName { get; set; }
        [XmlElement(ElementName = "RadioNr")]
        public int RadioNr { get; set; }
        [XmlElement(ElementName = "Freq")]
        public int Freq { get; set; }
        [XmlElement(ElementName = "TXFreq")]
        public int TXFreq { get; set; }
        [XmlElement(ElementName = "Mode")]
        public string Mode { get; set; }
        [XmlElement(ElementName = "OpCall")]
        public string OpCall { get; set; }
        [XmlElement(ElementName = "IsRunning")]
        public string IsRunning { get; set; }
        [XmlElement(ElementName = "FocusEntry")]
        public string FocusEntry { get; set; }
        [XmlElement(ElementName = "Antenna")]
        public int Antenna { get; set; }
        [XmlElement(ElementName = "Rotors")]
        public string Rotors { get; set; }
        [XmlElement(ElementName = "FocusRadioNr")]
        public int FocusRadioNr { get; set; }
        [XmlElement(ElementName = "IsStereo")]
        public string IsStereo { get; set; }
        [XmlElement(ElementName = "ActiveRadioNr")]
        public int ActiveRadioNr { get; set; }
    }

    // Helper class to parse XML datagrams
    public static class XmlConvert
    {
        public static T DeserializeObject<T>(string xml)
             where T : new()
        {
            if (string.IsNullOrEmpty(xml))
                return new T();
            try
            {
                using (var stringReader = new StringReader(xml))
                {
                    var serializer = new XmlSerializer(typeof(T));
                    return (T)serializer.Deserialize(stringReader);
                }
            }
            catch (Exception)
            {
                return new T();
            }
        }
    }

    public partial class MainWindow : Window
    {
        public const int ListenPort = 12060;
        public static string ComPort = "COM2";
        public static byte TrxAddress = 0x98;
        public static byte EdgeSet = 0x03; // which scope edge should be manipulated
        public static byte ResponseTime = 100; // Milliseconds to wait before reading response from radio
        public static byte[] ReadBuffer = new byte[100]; // Dummy read buffer. Much larger than needed.

        // Pre-baked CI-V commands
        public static byte[] CIVSetFixedMode = new byte[] { 0xFE, 0xFE, TrxAddress, 0xE0, 0x27, 0x14, 0x0, 0x1, 0xFD };
        public static byte[] CIVSetEdgeSet = new byte[] { 0xFE, 0xFE, TrxAddress, 0xE0, 0x27, 0x16, 0x0, EdgeSet, 0xFD };
        public static byte[] CIVSetRefLevel = new byte[] { 0xFE, 0xFE, TrxAddress, 0xE0, 0x27, 0x19, 0x00, 0x00, 0x00, 0x00, 0xFD };

        // Maps MHz to band name
        public static string[] bandName = new string[52] 
        { "?m", "160m", "?m", "80m", "?m", "60m", "?m", "40m", "?m", "?m", "30m", "?m", "?m",
            "?m", "20m", "?m", "?m", "?m", "17m", "?m", "?m", "15m", "?m", "?m", "12m", "?m",
            "?m", "?m", "10m", "10m", "?m", "?m", "?m", "?m", "?m", "?m", "?m", "?m", "?m",
            "?m", "?m", "?m", "?m", "?m", "?m", "?m", "?m", "?m", "?m", "?m", "6m", "6m" };

        // Maps MHz to internal band index
        public static int[] bandIndex = new int[52] 
        { 0, 0, 0, 1, 0, 2, 0, 3, 0, 0, 4, 0, 0, 0, 5, 0, 0, 0, 6, 0, 0, 7, 0, 0, 8, 0,
            0, 0, 9, 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 10 };

        // Maps actual MHz to radio's scope edge set on ICOM 7800, 785x, 7300 and 7610
        int[] RadioEdgeSet = new int[]
        { 1, 2, 3, 3, 3, 3, 4, 4, 5, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7, 7, 8, 8, 9, 9, 9, 9, 10, 10, 10, 10, 11,
            11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12 };

        // Waterfall edges and per mode/band segment ref levels 
        // _scopedge[Radionumber-1, i, modeindex, lower/upper/ref level]
        // converted at initialization to ScopeEdge[Radionumber-1, megaHertz, modeindex, lower/upper/ref level]
        // Mode is CW, Digital, Phone, Band = 0, 1, 2, 3
        int[] lowerEdgeCW = new int[52]; int[] upperEdgeCW = new int[52]; int[] refLevelCW = new int[52];
        int[] lowerEdgePh = new int[52]; int[] upperEdgePh = new int[52]; int[] refLevelPh = new int[52];
        int[] lowerEdgeDig = new int[52]; int[] upperEdgeDig = new int[52]; int[] refLevelDig = new int[52];

        public int currentLowerEdge, currentUpperEdge, currentRefLevel, newMHz, currentMHz = 0;
        public string currentMode = "", newMode = "";

        SerialPort port = new SerialPort(ComPort, 19200, Parity.None, 8, StopBits.One);

        public MainWindow()
        {
            string message;

            try
            {
                port.Open();
            }
            catch
            {
                MessageBoxResult result = MessageBox.Show("Could not open serial port " + ComPort, 
                    "ICOM Automagic", MessageBoxButton.OK, MessageBoxImage.Question);
                if (result == MessageBoxResult.OK)
                {
                    Application.Current.Shutdown();
                }
            }

            // Fetch window location from saved settings
            this.Top = Properties.Settings.Default.Top;
            this.Left = Properties.Settings.Default.Left;

            // Fetch lower and upper edges and ref levels from saved settings, ugly due to limitations in WPF settings
            lowerEdgeCW = Properties.Settings.Default.LowerEdgesCW.Split(';').Select(s => Int32.Parse(s)).ToArray();
            upperEdgeCW = Properties.Settings.Default.UpperEdgesCW.Split(';').Select(s => Int32.Parse(s)).ToArray();
            refLevelCW = Properties.Settings.Default.RefLevelsCW.Split(';').Select(s => Int32.Parse(s)).ToArray();

            lowerEdgePh = Properties.Settings.Default.LowerEdgesPh.Split(';').Select(s => Int32.Parse(s)).ToArray();
            upperEdgePh = Properties.Settings.Default.UpperEdgesPh.Split(';').Select(s => Int32.Parse(s)).ToArray();
            refLevelPh = Properties.Settings.Default.RefLevelsPh.Split(';').Select(s => Int32.Parse(s)).ToArray();

            lowerEdgeDig = Properties.Settings.Default.LowerEdgesDig.Split(';').Select(s => Int32.Parse(s)).ToArray();
            upperEdgeDig = Properties.Settings.Default.UpperEdgesDig.Split(';').Select(s => Int32.Parse(s)).ToArray();
            refLevelDig = Properties.Settings.Default.RefLevelsDig.Split(';').Select(s => Int32.Parse(s)).ToArray();

            InitializeComponent();

            Task.Run(async () =>
            {
                using (var udpClient = new UdpClient(ListenPort))
                {
                    while (true)
                    {
                        //IPEndPoint object will allow us to read datagrams sent from any source.
                        var receivedResults = await udpClient.ReceiveAsync();
                        message = Encoding.ASCII.GetString(receivedResults.Buffer);

                        XDocument doc = XDocument.Parse(message);

                        if (doc.Element("RadioInfo") != null)
                        {
                            RadioInfo radioInfo = new RadioInfo();
                            radioInfo = XmlConvert.DeserializeObject<RadioInfo>(message);

                            if (radioInfo.ActiveRadioNr == radioInfo.RadioNr)
                            {
                                newMHz = (int)(radioInfo.Freq / 100000f);

                                switch (radioInfo.Mode)
                                {
                                    case "CW":
                                        currentRefLevel = refLevelCW[bandIndex[newMHz]];
                                        currentUpperEdge = upperEdgeCW[bandIndex[newMHz]];
                                        currentLowerEdge = lowerEdgeCW[bandIndex[newMHz]];
                                        newMode = "CW";
                                        break;
                                    case "USB":
                                    case "LSB":
                                    case "AM":
                                        currentRefLevel = refLevelPh[bandIndex[newMHz]];
                                        currentUpperEdge = upperEdgePh[bandIndex[newMHz]];
                                        currentLowerEdge = lowerEdgePh[bandIndex[newMHz]];
                                        newMode = "Phone";
                                        break;
                                    default:
                                        currentRefLevel = refLevelDig[bandIndex[newMHz]];
                                        currentUpperEdge = upperEdgeDig[bandIndex[newMHz]];
                                        currentLowerEdge = lowerEdgeDig[bandIndex[newMHz]];
                                        newMode = "Digital";
                                        break;
                                }

                                Application.Current.Dispatcher.Invoke(new Action(() =>
                                {
                                    BandModeLabel.Content = string.Format("{0,4} {1,8}", bandName[newMHz], newMode);
                                    RefLevelLabel.Content = string.Format("Ref: {0:+#;-#;0}dB", currentRefLevel);

                                    if ((newMHz != currentMHz) || (newMode != currentMode))
                                    {
                                        LowerEdgeTextbox.Text = currentLowerEdge.ToString();
                                        UpperEdgeTextbox.Text = currentUpperEdge.ToString();
                                        RefLevelSlider.Value = currentRefLevel;

                                        SetupRadio_Edges(currentLowerEdge, currentUpperEdge, RadioEdgeSet[newMHz]);
                                        SetupRadio_Reflevel(currentRefLevel);

                                    }

                                    currentMHz = newMHz;
                                    currentMode = newMode;
                                }));
                            }
                        }
                    }
                }
            });
        }

        private void EdgeTextboxKeydown(object sender, KeyEventArgs e)
        {
            int lower_edge = 0, upper_edge = 0;


                if (e.Key == Key.Return)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        RefLevelLabel.Content = string.Format("Ref: {0:+#;-#;0}dB", currentRefLevel);

                        if (sender == LowerEdgeTextbox)
                            UpperEdgeTextbox.Focus();
                        else
                            LowerEdgeTextbox.Focus();

                        try
                        {
                            lower_edge = Int32.Parse(LowerEdgeTextbox.Text);
                            upper_edge = Int32.Parse(UpperEdgeTextbox.Text);
                        }
                        catch { };

                        switch (currentMode)
                        {
                            case "CW":
                                lowerEdgeCW[bandIndex[currentMHz]] = lower_edge;
                                upperEdgeCW[bandIndex[currentMHz]] = upper_edge;
                                break;
                            case "Phone":
                                lowerEdgePh[bandIndex[currentMHz]] = lower_edge;
                                upperEdgePh[bandIndex[currentMHz]] = upper_edge;
                                break;
                            default:
                                lowerEdgeDig[bandIndex[currentMHz]] = lower_edge;
                                upperEdgeDig[bandIndex[currentMHz]] = upper_edge;
                                break;
                        }
                    }));

                    SetupRadio_Edges(lower_edge, upper_edge, RadioEdgeSet[currentMHz]);
                }
        }

        private void SaveLocation(object sender, EventArgs e)
        {
            // Remember window location 
            Properties.Settings.Default.Top = this.Top;
            Properties.Settings.Default.Left = this.Left;
            Properties.Settings.Default.Save();
        }

        private void SaveSettings(object sender, EventArgs e)
        {
            Properties.Settings.Default.LowerEdgesCW = String.Join(";", lowerEdgeCW.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.UpperEdgesCW = String.Join(";", upperEdgeCW.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.RefLevelsCW = String.Join(";", refLevelCW.Select(i => i.ToString()).ToArray());

            Properties.Settings.Default.LowerEdgesPh = String.Join(";", lowerEdgePh.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.UpperEdgesPh = String.Join(";", upperEdgePh.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.RefLevelsPh = String.Join(";", refLevelPh.Select(i => i.ToString()).ToArray());

            Properties.Settings.Default.LowerEdgesDig = String.Join(";", lowerEdgeDig.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.UpperEdgesDig = String.Join(";", upperEdgeDig.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.RefLevelsDig = String.Join(";", refLevelDig.Select(i => i.ToString()).ToArray());

            Properties.Settings.Default.Save();
        }

        void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            currentRefLevel = (int)RefLevelSlider.Value;

            switch (currentMode)
            {
                case "CW":
                    refLevelCW[bandIndex[newMHz]] = currentRefLevel;
                    break;
                case "Phone":
                    refLevelPh[bandIndex[newMHz]] = currentRefLevel;
                    break;
                default:
                    refLevelDig[bandIndex[newMHz]] = currentRefLevel;
                    break;
            }

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                //RefLevelLabel.Content = string.Format("{0,4}dB", currentRefLevel);
            }));

            SetupRadio_Reflevel((int)currentRefLevel);
        }

        void SetupRadio_Edges(int lower_edge, int upper_edge, int ICOMedgeSegment)
        {
            byte[] CIVSetEdges = new byte[19]
            {
                0xFE, 0xFE, TrxAddress, 0xE0,
                0x27, 0x1E,
                (byte)((ICOMedgeSegment / 10) * 16 + (ICOMedgeSegment % 10)),
                EdgeSet,
                0x00, // Lower 10Hz & 1Hz
                (byte)((lower_edge % 10) * 16 + 0), // 1kHz & 100Hz
                (byte)(((lower_edge / 100) % 10) * 16 + ((lower_edge / 10) % 10)), // 100kHz & 10kHz
                (byte)(((lower_edge / 10000) % 10) * 16 + (lower_edge / 1000) % 10), // 10MHz & 1MHz
                0x00, // 1GHz & 100MHz
                0x00, // // Upper 10Hz & 1Hz 
                (byte)((upper_edge % 10) * 16 + 0), // 1kHz & 100Hz
                (byte)(((upper_edge / 100) % 10) * 16 + (upper_edge / 10) % 10), // 100kHz & 10kHz
                (byte)(((upper_edge / 10000) % 10) * 16 + (upper_edge / 1000) % 10), // 10MHz & 1MHz
                0x00, // 1GHz & 100MHz
                0xFD
            };

            //DisplayWaterfallEdges.Content = lower_edge.ToString("N0") + " - " + upper_edge.ToString("N0") + "kHz";

            port.Write(CIVSetFixedMode, 0, CIVSetFixedMode.Length); // Set fixed mode
            //System.Threading.Thread.Sleep(ResponseTime); // Wait
            //port.Read(ReadBuffer, 0, port.BytesToRead); // Flush response including echo

            port.Write(CIVSetEdgeSet, 0, CIVSetEdgeSet.Length); // set edge set EdgeSet
            //System.Threading.Thread.Sleep(ResponseTime); // Wait
            //port.Read(ReadBuffer, 0, port.BytesToRead); // Flush response including echo

            port.Write(CIVSetEdges, 0, CIVSetEdges.Length); // set edge set EdgeSet
            System.Threading.Thread.Sleep(ResponseTime); // Wait
            //port.Read(ReadBuffer, 0, port.BytesToRead); // Flush response including echo
        }

        void SetupRadio_Reflevel(int ref_level) 
        {
            int absRefLevel = (ref_level >= 0) ? ref_level : -ref_level;

            CIVSetRefLevel[7] = (byte)((absRefLevel / 10) * 16 + absRefLevel % 10);
            CIVSetRefLevel[9] = (ref_level >= 0) ? (byte)0 : (byte)1;
            
            port.Write(CIVSetRefLevel, 0, CIVSetRefLevel.Length); // set edge set EdgeSet
            //System.Threading.Thread.Sleep(ResponseTime); // Wait
            //port.Read(ReadBuffer, 0, port.BytesToRead); // Flush response including echo

        }

    }
}
