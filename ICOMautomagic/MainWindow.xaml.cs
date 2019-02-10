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
using System.Windows.Media;

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
        public readonly bool NoRadio = false;
        public const int ListenPort = 12060;
        public static byte TrxAddress = 0x98;
        public static int ZoomRange = 20; // Range of zoomed waterfall in kHz
        public static byte EdgeSet = 0x03; // which scope edge should be manipulated
        public static SolidColorBrush ActiveButtonColor = Brushes.LightGreen; // Color for active button
        public static SolidColorBrush PassiveButtonColor = Brushes.LightGray; // Color for passive button
        public static string BandModeLabelFormat = "{0,-4}{1,5}";
        public static string RefLabelFormat = "Ref: {0:+#;-#;0}dB";

        //public static byte ResponseTime = 100; // Milliseconds to wait before reading response from radio
        //public static byte[] ReadBuffer = new byte[100]; // Dummy read buffer. Much larger than needed.

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
        int[] lowerEdgeCW = new int[52]; int[] upperEdgeCW = new int[52]; int[] refLevelCW = new int[52]; int[] refLevelCWZ = new int[52];
        int[] lowerEdgeSSB = new int[52]; int[] upperEdgeSSB = new int[52]; int[] refLevelSSB = new int[52]; int[] refLevelSSBZ = new int[52];
        int[] lowerEdgeDigital = new int[52]; int[] upperEdgeDigital = new int[52]; int[] refLevelDigital = new int[52]; int[] refLevelDigitalZ = new int[52];

        public int currentLowerEdge, currentUpperEdge, currentRefLevel, currentFrequency = 0, newMHz, currentMHz = 0;
        public string currentMode = "", newMode = "", ComPort;
        public bool Zoomed = false;

        public SerialPort port; 

        public MainWindow()
        {
            string message;
            String[] commandLineArguments = Environment.GetCommandLineArgs();

            InitializeComponent();

            // If there is a command line argument, take it as the COM port 
            if (commandLineArguments.Length > 1)
                ComPort = commandLineArguments[1].ToUpper();
            else
                ComPort = Properties.Settings.Default.COMport;

            ProgramWindow.Title = "ICOM Automatic (" + ComPort + ")";


            if (!NoRadio)
            {
                port = new SerialPort(ComPort, 19200, Parity.None, 8, StopBits.One);

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
            }


            // Fetch window location from saved settings
            this.Top = Properties.Settings.Default.Top;
            this.Left = Properties.Settings.Default.Left;

            // Fetch lower and upper edges and ref levels from saved settings, ugly due to limitations in WPF settings
            lowerEdgeCW = Properties.Settings.Default.LowerEdgesCW.Split(';').Select(s => Int32.Parse(s)).ToArray();
            upperEdgeCW = Properties.Settings.Default.UpperEdgesCW.Split(';').Select(s => Int32.Parse(s)).ToArray();
            refLevelCW = Properties.Settings.Default.RefLevelsCW.Split(';').Select(s => Int32.Parse(s)).ToArray();
            refLevelCWZ = Properties.Settings.Default.RefLevelsCWZ.Split(';').Select(s => Int32.Parse(s)).ToArray();

            lowerEdgeSSB = Properties.Settings.Default.LowerEdgesSSB.Split(';').Select(s => Int32.Parse(s)).ToArray();
            upperEdgeSSB = Properties.Settings.Default.UpperEdgesSSB.Split(';').Select(s => Int32.Parse(s)).ToArray();
            refLevelSSB = Properties.Settings.Default.RefLevelsSSB.Split(';').Select(s => Int32.Parse(s)).ToArray();
            refLevelSSBZ = Properties.Settings.Default.RefLevelsSSBZ.Split(';').Select(s => Int32.Parse(s)).ToArray();

            lowerEdgeDigital = Properties.Settings.Default.LowerEdgesDigital.Split(';').Select(s => Int32.Parse(s)).ToArray();
            upperEdgeDigital = Properties.Settings.Default.UpperEdgesDigital.Split(';').Select(s => Int32.Parse(s)).ToArray();
            refLevelDigital = Properties.Settings.Default.RefLevelsDigitalZ.Split(';').Select(s => Int32.Parse(s)).ToArray();


            ZoomButton.Content = string.Format("+/-{0}kHz", (int)(ZoomRange / 2));
            BandModeButton.Background = PassiveButtonColor;
            ZoomButton.Background = PassiveButtonColor;

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
                                currentFrequency = (int)(radioInfo.Freq / 100f);

                                switch (radioInfo.Mode)
                                {
                                    case "CW":
                                        newMode = "CW";
                                        break;
                                    case "USB":
                                    case "LSB":
                                        newMode = "SSB";
                                        break;
                                    default:
                                        newMode = "Digital";
                                        break;
                                }

                                // Only update radio when mode or band changes to not override manual changes
                                if ((newMHz != currentMHz) || (newMode != currentMode))
                                {
                                    switch (newMode)
                                    {
                                        case "CW":
                                            currentRefLevel = refLevelCW[bandIndex[newMHz]];
                                            currentUpperEdge = upperEdgeCW[bandIndex[newMHz]];
                                            currentLowerEdge = lowerEdgeCW[bandIndex[newMHz]];
                                            break;
                                        case "SSB":
                                            currentRefLevel = refLevelSSB[bandIndex[newMHz]];
                                            currentUpperEdge = upperEdgeSSB[bandIndex[newMHz]];
                                            currentLowerEdge = lowerEdgeSSB[bandIndex[newMHz]];
                                            break;
                                        default:
                                            currentRefLevel = refLevelDigital[bandIndex[newMHz]];
                                            currentUpperEdge = upperEdgeDigital[bandIndex[newMHz]];
                                            currentLowerEdge = lowerEdgeDigital[bandIndex[newMHz]];
                                            break;
                                    }

                                    Application.Current.Dispatcher.Invoke(new Action(() =>
                                    {
                                        BandModeLabel.Content = string.Format(BandModeLabelFormat, bandName[newMHz], newMode);
                                        RefLevelLabel.Content = string.Format(RefLabelFormat, currentRefLevel);

                                        // Highlight band-mode button
                                        ZoomButton.Background = PassiveButtonColor;
                                        BandModeButton.Background = ActiveButtonColor;

                                        // Update displayed information
                                        LowerEdgeTextbox.Text = currentLowerEdge.ToString();
                                        UpperEdgeTextbox.Text = currentUpperEdge.ToString();
                                        RefLevelSlider.Value = currentRefLevel;

                                        // Update waterfall edges and ref level in radio
                                        SetupRadio_Edges(currentLowerEdge, currentUpperEdge, RadioEdgeSet[newMHz]);
                                        SetupRadio_Reflevel(currentRefLevel);

                                        currentMHz = newMHz;
                                        currentMode = newMode;
                                        Zoomed = false;
                                    }));
                                }
                            }
                        }
                    }
                }
            });
        }

        // On hitting a key in upper and lower edge text boxes
        private void OnEdgeTextboxKeydown(object sender, KeyEventArgs e)
        {
            int lower_edge = 0, upper_edge = 0;

            if (e.Key == Key.Return)
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    // Toggle focus betwen the two entry text boxes
                    if (sender == LowerEdgeTextbox)
                        UpperEdgeTextbox.Focus();
                    else
                        LowerEdgeTextbox.Focus();

                    try // Catch and ignore parsing errors
                    {
                        lower_edge = Int32.Parse(LowerEdgeTextbox.Text);
                        upper_edge = Int32.Parse(UpperEdgeTextbox.Text);
                    }
                    catch
                    {
                        return;
                    };

                    switch (currentMode)
                    {
                        case "CW":
                            lowerEdgeCW[bandIndex[currentMHz]] = lower_edge;
                            upperEdgeCW[bandIndex[currentMHz]] = upper_edge;
                            currentRefLevel = refLevelCW[bandIndex[currentMHz]];
                            break;
                        case "SSB":
                            lowerEdgeSSB[bandIndex[currentMHz]] = lower_edge;
                            upperEdgeSSB[bandIndex[currentMHz]] = upper_edge;
                            currentRefLevel = refLevelSSB[bandIndex[currentMHz]];
                            break;
                        default:
                            lowerEdgeDigital[bandIndex[currentMHz]] = lower_edge;
                            upperEdgeDigital[bandIndex[currentMHz]] = upper_edge;
                            currentRefLevel = refLevelDigital[bandIndex[currentMHz]];
                            break;
                    }
                }));
                RefLevelLabel.Content = string.Format(RefLabelFormat, currentRefLevel);

                Zoomed = false;
                ZoomButton.Background = PassiveButtonColor;
                BandModeButton.Background = ActiveButtonColor;

                SetupRadio_Edges(lower_edge, upper_edge, RadioEdgeSet[currentMHz]);
                SetupRadio_Reflevel(currentRefLevel);
            }
        }

        // On band-mode button clicked
        private void OnBandModeButton(object sender, RoutedEventArgs e)
        {
            if (currentLowerEdge != 0) // Do nothing if we have not yet received radioInfo
            {
                SetupRadio_Edges(currentLowerEdge, currentUpperEdge, RadioEdgeSet[currentMHz]);

                switch (currentMode)
                {
                    case "CW":
                        currentRefLevel = refLevelCW[bandIndex[currentMHz]];
                        break;
                    case "SSB":
                        currentRefLevel = refLevelSSB[bandIndex[currentMHz]];
                        break;
                    default:
                        currentRefLevel = refLevelDigital[bandIndex[currentMHz]];
                        break;
                }

                SetupRadio_Reflevel(currentRefLevel);
                Zoomed = false;

                Application.Current.Dispatcher.Invoke(new Action(() => {
                    if (RefLevelLabel != null)
                    {
                        RefLevelLabel.Content = string.Format(RefLabelFormat, currentRefLevel);
                        RefLevelSlider.Value = currentRefLevel;
                        LowerEdgeTextbox.Text = currentLowerEdge.ToString();
                        UpperEdgeTextbox.Text = currentUpperEdge.ToString();
                        ZoomButton.Background = PassiveButtonColor;
                        BandModeButton.Background = ActiveButtonColor;
                    }
                }));
            }
        }

        // Save all settings when closing program
        private void OnClosing(object sender, EventArgs e)
        {
            Properties.Settings.Default.LowerEdgesCW = String.Join(";", lowerEdgeCW.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.UpperEdgesCW = String.Join(";", upperEdgeCW.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.RefLevelsCW = String.Join(";", refLevelCW.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.RefLevelsCWZ = String.Join(";", refLevelCWZ.Select(i => i.ToString()).ToArray());

            Properties.Settings.Default.LowerEdgesSSB = String.Join(";", lowerEdgeSSB.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.UpperEdgesSSB = String.Join(";", upperEdgeSSB.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.RefLevelsSSB = String.Join(";", refLevelSSB.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.RefLevelsSSBZ = String.Join(";", refLevelSSBZ.Select(i => i.ToString()).ToArray());

            Properties.Settings.Default.LowerEdgesDigital = String.Join(";", lowerEdgeDigital.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.UpperEdgesDigital = String.Join(";", upperEdgeDigital.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.RefLevelsDigital = String.Join(";", refLevelDigital.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.RefLevelsDigitalZ = String.Join(";", refLevelDigitalZ.Select(i => i.ToString()).ToArray());

            Properties.Settings.Default.COMport = ComPort;

            Properties.Settings.Default.Save();
        }

        private void OnZoomButton(object sender, RoutedEventArgs e)
        {
            int loweredge, upperedge;

            if (currentFrequency != 0)
            {
                loweredge = currentFrequency - ZoomRange / 2;
                upperedge = loweredge + ZoomRange;

                SetupRadio_Edges(loweredge, upperedge, RadioEdgeSet[currentMHz]);

                switch (currentMode)
                {
                    case "CW":
                        currentRefLevel = refLevelCWZ[bandIndex[currentMHz]];
                        break;
                    case "SSB":
                        currentRefLevel = refLevelSSBZ[bandIndex[currentMHz]];
                        break;
                    default:
                        currentRefLevel = refLevelDigitalZ[bandIndex[currentMHz]];
                        break;
                }

                SetupRadio_Reflevel(currentRefLevel);
                Zoomed = true;

                Application.Current.Dispatcher.Invoke(new Action(() => {
                    if (RefLevelLabel != null)
                    {
                        RefLevelLabel.Content = string.Format(RefLabelFormat, currentRefLevel);
                        RefLevelSlider.Value = currentRefLevel;
                        LowerEdgeTextbox.Text = loweredge.ToString();
                        UpperEdgeTextbox.Text = upperedge.ToString();
                        ZoomButton.Background = ActiveButtonColor;
                        BandModeButton.Background = PassiveButtonColor;
                    }
                }));

            }
        }

        // On arrow key modification of slider
        private void OnSliderKey(object sender, KeyEventArgs e)
        {
            UpdateSlider();
        }

        // On mouse modification of slider
        private void OnSliderMouse(object sender, MouseButtonEventArgs e)
        {
            UpdateSlider();
        }

        void UpdateSlider()
        {
            currentRefLevel = (int)(RefLevelSlider.Value + 0.0f);
            ;

            switch (currentMode)
            {
                case "CW":
                    if (Zoomed)
                        refLevelCWZ[bandIndex[currentMHz]] = currentRefLevel;
                    else
                        refLevelCW[bandIndex[currentMHz]] = currentRefLevel;
                    break;
                case "SSB":
                    if (Zoomed)
                        refLevelSSBZ[bandIndex[currentMHz]] = currentRefLevel;
                    else
                        refLevelSSB[bandIndex[currentMHz]] = currentRefLevel;
                    break;
                default:
                    if (Zoomed)
                        refLevelDigitalZ[bandIndex[currentMHz]] = currentRefLevel;
                    else
                        refLevelDigital[bandIndex[currentMHz]] = currentRefLevel;
                    break;
            }

            Application.Current.Dispatcher.Invoke(new Action(() => {
                if (RefLevelLabel != null)
                    RefLevelLabel.Content = string.Format(RefLabelFormat, currentRefLevel);
            }));

            SetupRadio_Reflevel((int)currentRefLevel);
        }

        // On movement of window
        private void OnLocationChange(object sender, EventArgs e)
        {
            // Remember window location 
            Properties.Settings.Default.Top = this.Top;
            Properties.Settings.Default.Left = this.Left;
            Properties.Settings.Default.Save();
        }

        // Update radio with new waterfall edges
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

            if (!NoRadio)
            {
                port.Write(CIVSetFixedMode, 0, CIVSetFixedMode.Length); // Set fixed mode
                //System.Threading.Thread.Sleep(ResponseTime); // Wait

                port.Write(CIVSetEdgeSet, 0, CIVSetEdgeSet.Length); // set edge set EdgeSet
                //System.Threading.Thread.Sleep(ResponseTime); // Wait
                //port.Read(ReadBuffer, 0, port.BytesToRead); // Flush response including echo

                port.Write(CIVSetEdges, 0, CIVSetEdges.Length); // set edge set EdgeSet
                //System.Threading.Thread.Sleep(ResponseTime); // Wait
                //port.Read(ReadBuffer, 0, port.BytesToRead); // Flush response including echo
            }
        }

        // Update radio with new REF level
        void SetupRadio_Reflevel(int ref_level) 
        {
            int absRefLevel = (ref_level >= 0) ? ref_level : -ref_level;

            CIVSetRefLevel[7] = (byte)((absRefLevel / 10) * 16 + absRefLevel % 10);
            CIVSetRefLevel[9] = (ref_level >= 0) ? (byte)0 : (byte)1;
            
            if (!NoRadio)
                port.Write(CIVSetRefLevel, 0, CIVSetRefLevel.Length); // set edge set EdgeSet
            //System.Threading.Thread.Sleep(ResponseTime); // Wait
            //port.Read(ReadBuffer, 0, port.BytesToRead); // Flush response including echo

        }
    }
}
