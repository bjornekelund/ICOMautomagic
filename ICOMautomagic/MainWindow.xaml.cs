// Companion app for N1MM when using ICOM radios with waterfall display,
// e.g. IC-7300, IC-7610, and IC-785x.
// 
// By Björn Ekelund SM7IUN sm7iun@ssa.se

using System;
using System.Linq;
using System.Net.Sockets;
using System.Xml.Linq;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Text;
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
        readonly bool NoRadio = false; // For debugging with no radio attached
        const int ListenPort = 12060; // UDP broadcast port
        const byte TrxAddress = 0x98; // Address of IC-7610
        const int ZoomRange = 20; // Range of zoomed waterfall in kHz
        const byte EdgeSet = 0x03; // which scope edge should be manipulated
        const int PortSpeed = 19200; // CI-V port speed

        static SolidColorBrush SpecialGreen = (SolidColorBrush)(new BrushConverter().ConvertFrom("#ff58f049"));
        static SolidColorBrush ActiveColor = SpecialGreen; // Color for active button
        static SolidColorBrush PassiveColor = Brushes.LightGray; // Color for passive button
        static SolidColorBrush BarefootColor = Brushes.DarkGreen; // Color for power label when barefoot
        static SolidColorBrush ExciterColor = Brushes.Black; // Color for power label when using PA
        static SolidColorBrush BandModeColor = Brushes.Blue; // Color for valid band and mode display

        // Pre-baked CI-V commands
        static byte[] CIVSetFixedMode = new byte[] { 0xFE, 0xFE, TrxAddress, 0xE0, 0x27, 0x14, 0x0, 0x1, 0xFD };
        static byte[] CIVSetEdgeSet = new byte[] { 0xFE, 0xFE, TrxAddress, 0xE0, 0x27, 0x16, 0x0, EdgeSet, 0xFD };
        static byte[] CIVSetRefLevel = new byte[] { 0xFE, 0xFE, TrxAddress, 0xE0, 0x27, 0x19, 0x00, 0x00, 0x00, 0x00, 0xFD };
        static byte[] CIVSetPwrLevel = new byte[] { 0xFE, 0xFE, TrxAddress, 0xE0, 0x14, 0x0A, 0x00, 0x00, 0xFD };

        // Maps MHz to band name
        static string[] bandName = new string[52]
        { "?m", "160m", "?m", "80m", "?m", "60m", "?m", "40m", "?m", "?m", "30m", "?m", "?m",
            "?m", "20m", "?m", "?m", "?m", "17m", "?m", "?m", "15m", "?m", "?m", "12m", "?m",
            "?m", "?m", "10m", "10m", "?m", "?m", "?m", "?m", "?m", "?m", "?m", "?m", "?m",
            "?m", "?m", "?m", "?m", "?m", "?m", "?m", "?m", "?m", "?m", "?m", "6m", "6m" };

        // Maps MHz to internal band index
        public static int[] bandIndex = new int[52]
        { 0, 0, 0, 1, 1, 2, 2, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6, 7, 7, 7, 8, 8,
            8, 8, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10 };

        // Maps actual MHz to radio's scope edge set on ICOM 7800, 785x, 7300 and 7610
        int[] RadioEdgeSet = new int[]
        { 1, 2, 3, 3, 3, 3, 4, 4, 5, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7, 7, 8, 8, 9, 9, 9, 9, 10, 10, 10, 10, 11,
            11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12 };

        // Per mode/band waterfall edges and ref levels. Also one zoomed ref level per band.
        int[] lowerEdgeCW = new int[11]; int[] upperEdgeCW = new int[11]; int[] refLevelCW = new int[11]; int[] refLevelCWZ = new int[11];
        int[] lowerEdgeSSB = new int[11]; int[] upperEdgeSSB = new int[11]; int[] refLevelSSB = new int[11]; int[] refLevelSSBZ = new int[11];
        int[] lowerEdgeDigital = new int[11]; int[] upperEdgeDigital = new int[11]; int[] refLevelDigital = new int[11]; int[] refLevelDigitalZ = new int[11];

        int[] pwrLevelCW = new int[11]; int[] pwrLevelSSB = new int[11]; int[] pwrLevelDigital = new int[11];

        // Global variables
        int currentLowerEdge, currentUpperEdge, currentRefLevel, currentPwrLevel, currentFrequency = 0, newMHz, currentMHz = 0;
        string currentMode = string.Empty, newMode = string.Empty, ComPort;
        bool Zoomed, RadioInfoReceived, Barefoot;
        SerialPort port;

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


            if (!NoRadio) // If we are not debugging, open serial port
            {
                ProgramWindow.Title = "ICOM Automagic N1MM Logger+ (" + ComPort + ")";
                port = new SerialPort(ComPort, PortSpeed, Parity.None, 8, StopBits.One);

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

            // Fetch window location from last time
            this.Top = Properties.Settings.Default.Top;
            this.Left = Properties.Settings.Default.Left;

            // Fetch barefoot status from last time
            Barefoot = Properties.Settings.Default.Barefoot;

            // Fetch lower and upper edges and ref levels from last time, ugly solution due to limitations in WPF settings management
            lowerEdgeCW = Properties.Settings.Default.LowerEdgesCW.Split(';').Select(s => Int32.Parse(s)).ToArray();
            upperEdgeCW = Properties.Settings.Default.UpperEdgesCW.Split(';').Select(s => Int32.Parse(s)).ToArray();
            refLevelCW = Properties.Settings.Default.RefLevelsCW.Split(';').Select(s => Int32.Parse(s)).ToArray();
            refLevelCWZ = Properties.Settings.Default.RefLevelsCWZ.Split(';').Select(s => Int32.Parse(s)).ToArray();
            pwrLevelCW = Properties.Settings.Default.PwrLevelsCW.Split(';').Select(s => Int32.Parse(s)).ToArray();

            lowerEdgeSSB = Properties.Settings.Default.LowerEdgesSSB.Split(';').Select(s => Int32.Parse(s)).ToArray();
            upperEdgeSSB = Properties.Settings.Default.UpperEdgesSSB.Split(';').Select(s => Int32.Parse(s)).ToArray();
            refLevelSSB = Properties.Settings.Default.RefLevelsSSB.Split(';').Select(s => Int32.Parse(s)).ToArray();
            refLevelSSBZ = Properties.Settings.Default.RefLevelsSSBZ.Split(';').Select(s => Int32.Parse(s)).ToArray();
            pwrLevelSSB = Properties.Settings.Default.PwrLevelsSSB.Split(';').Select(s => Int32.Parse(s)).ToArray();

            lowerEdgeDigital = Properties.Settings.Default.LowerEdgesDigital.Split(';').Select(s => Int32.Parse(s)).ToArray();
            upperEdgeDigital = Properties.Settings.Default.UpperEdgesDigital.Split(';').Select(s => Int32.Parse(s)).ToArray();
            refLevelDigital = Properties.Settings.Default.RefLevelsDigitalZ.Split(';').Select(s => Int32.Parse(s)).ToArray();
            pwrLevelDigital = Properties.Settings.Default.PwrLevelsDigital.Split(';').Select(s => Int32.Parse(s)).ToArray();

            // Set Zoom button text based on value of ZoomRange
            ZoomButton.Content = string.Format("±{0}kHz", (int)(ZoomRange / 2));

            // Set Band-mode button active, Zoom button inactive
            Zoomed = false;
            BandModeButton.Background = ActiveColor;
            BandModeButton.BorderBrush = ActiveColor;
            ZoomButton.Background = PassiveColor;
            ZoomButton.BorderBrush = PassiveColor;
            
            // To disable functions until we have received info from N1MM
            RadioInfoReceived = false; 

            Task.Run(async () =>
            {
                using (var udpClient = new UdpClient(ListenPort))
                {
                    while (true)
                    {
                        //Wait for UDP packets to arrive 
                        var receivedResults = await udpClient.ReceiveAsync();
                        message = Encoding.ASCII.GetString(receivedResults.Buffer);
                        XDocument doc = XDocument.Parse(message);

                        if (doc.Element("RadioInfo") != null) // If it is a RadioInfo datagram
                        {
                            // Parse XML into object radioInfo
                            RadioInfo radioInfo = new RadioInfo();
                            radioInfo = XmlConvert.DeserializeObject<RadioInfo>(message);

                            if (radioInfo.RadioNr == 1) // Only listen to RadioInfo for radio 1
                            {
                                newMHz = (int)(radioInfo.Freq / 100000f);
                                currentFrequency = (int)(radioInfo.Freq / 100f); // Make it kHz
                                RadioInfoReceived = true;

                                switch (radioInfo.Mode)
                                {
                                    case "CW":
                                        newMode = "CW";
                                        break;
                                    case "USB":
                                    case "LSB":
                                    case "AM":
                                        newMode = "SSB";
                                        break;
                                    default:
                                        newMode = "Digital";
                                        break;
                                }

                                // Only auto update radio when mode or band changes to avoid 
                                // overruling manual changes made on the radio's front panel
                                if ((newMHz != currentMHz) || (newMode != currentMode))
                                {
                                    currentMHz = newMHz;
                                    currentMode = newMode;

                                    switch (currentMode)
                                    {
                                        case "CW":
                                            currentLowerEdge = lowerEdgeCW[bandIndex[currentMHz]];
                                            currentUpperEdge = upperEdgeCW[bandIndex[currentMHz]];
                                            currentRefLevel = refLevelCW[bandIndex[currentMHz]];
                                            currentPwrLevel = pwrLevelCW[bandIndex[currentMHz]];
                                            break;
                                        case "SSB":
                                            currentLowerEdge = lowerEdgeSSB[bandIndex[currentMHz]];
                                            currentUpperEdge = upperEdgeSSB[bandIndex[currentMHz]];
                                            currentRefLevel = refLevelSSB[bandIndex[currentMHz]];
                                            currentPwrLevel = pwrLevelSSB[bandIndex[currentMHz]];
                                            break;
                                        default:
                                            currentLowerEdge = lowerEdgeDigital[bandIndex[currentMHz]];
                                            currentUpperEdge = upperEdgeDigital[bandIndex[currentMHz]];
                                            currentRefLevel = refLevelDigital[bandIndex[currentMHz]];
                                            currentPwrLevel = pwrLevelDigital[bandIndex[currentMHz]];
                                            break;
                                    }

                                    // Execute changes to the UI on main thread 
                                    Application.Current.Dispatcher.Invoke(new Action(() =>
                                    {
                                        // Highlight band-mode button and exit Zoomed mode if active
                                        Zoomed = false;
                                        ZoomButton.Background = PassiveColor;
                                        ZoomButton.BorderBrush = PassiveColor;
                                        BandModeButton.Background = ActiveColor;
                                        BandModeButton.BorderBrush = ActiveColor;

                                        // Allow entry in edge text boxes 
                                        LowerEdgeTextbox.IsEnabled = true;
                                        UpperEdgeTextbox.IsEnabled = true;

                                        // Update UI and waterfall edges and ref level in radio 
                                        UpdateRadioEdges(currentLowerEdge, currentUpperEdge, RadioEdgeSet[currentMHz]);
                                        UpdateRadioReflevel(currentRefLevel);
                                        UpdateRadioPwrlevel(currentPwrLevel);

                                        // Update band/mode display in UI
                                        BandLabel.Content = bandName[newMHz];
                                        BandLabel.Foreground = BandModeColor;
                                        ModeLabel.Content = newMode;
                                        ModeLabel.Foreground = BandModeColor;
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
            int lower, upper;

            if (!RadioInfoReceived) // Do nothing before we know the radio's frequency
                return;

            if (e.Key == Key.Return) // Only parse input when ENTER is hit 
            {

                try // Parse and ignore input if there are parsing errors
                {
                    lower = Int32.Parse(LowerEdgeTextbox.Text);
                    upper = Int32.Parse(UpperEdgeTextbox.Text);
                }
                catch
                {
                    return; // Ignore input if parsing failed
                }

                // We have a successful parse, assign values
                currentLowerEdge = lower;
                currentUpperEdge = upper;

                switch (currentMode)
                {
                    case "CW":
                        lowerEdgeCW[bandIndex[currentMHz]] = currentLowerEdge;
                        upperEdgeCW[bandIndex[currentMHz]] = currentUpperEdge;
                        currentRefLevel = refLevelCW[bandIndex[currentMHz]];
                        break;
                    case "SSB":
                        lowerEdgeSSB[bandIndex[currentMHz]] = currentLowerEdge;
                        upperEdgeSSB[bandIndex[currentMHz]] = currentUpperEdge;
                        currentRefLevel = refLevelSSB[bandIndex[currentMHz]];
                        break;
                    default:
                        lowerEdgeDigital[bandIndex[currentMHz]] = currentLowerEdge;
                        upperEdgeDigital[bandIndex[currentMHz]] = currentUpperEdge;
                        currentRefLevel = refLevelDigital[bandIndex[currentMHz]];
                        break;
                }

                UpdateRadioEdges(currentLowerEdge, currentUpperEdge, RadioEdgeSet[currentMHz]);
                UpdateRadioReflevel(currentRefLevel);

                // Toggle focus betwen the two entry text boxes
                if (sender == LowerEdgeTextbox)
                    UpperEdgeTextbox.Focus();
                else
                    LowerEdgeTextbox.Focus();
            }
        }

        // On band-mode button clicked
        private void OnBandModeButton(object sender, RoutedEventArgs e)
        {
            // Do nothing if we have not yet received information from N1MM
            if (!RadioInfoReceived) 
            return;

            switch (currentMode)
            {
                case "CW":
                    currentLowerEdge = lowerEdgeCW[bandIndex[currentMHz]];
                    currentUpperEdge = upperEdgeCW[bandIndex[currentMHz]];
                    currentRefLevel = refLevelCW[bandIndex[currentMHz]];
                    break;
                case "SSB":
                    currentLowerEdge = lowerEdgeSSB[bandIndex[currentMHz]];
                    currentUpperEdge = upperEdgeSSB[bandIndex[currentMHz]];
                    currentRefLevel = refLevelSSB[bandIndex[currentMHz]];
                    break;
                default: // All other modes = Digital 
                    currentLowerEdge = lowerEdgeDigital[bandIndex[currentMHz]];
                    currentUpperEdge = upperEdgeDigital[bandIndex[currentMHz]];
                    currentRefLevel = refLevelDigital[bandIndex[currentMHz]];
                    break;
            }

            UpdateRadioEdges(currentLowerEdge, currentUpperEdge, RadioEdgeSet[currentMHz]);

            UpdateRadioReflevel(currentRefLevel);
            Zoomed = false;
            LowerEdgeTextbox.IsEnabled = true;
            UpperEdgeTextbox.IsEnabled = true;

            ZoomButton.Background = PassiveColor;
            ZoomButton.BorderBrush = PassiveColor;
            BandModeButton.Background = ActiveColor;
            BandModeButton.BorderBrush = ActiveColor;
        }

        // Save all settings when closing program
        private void OnClosing(object sender, EventArgs e)
        {
            // Remember window location 
            Properties.Settings.Default.Top = this.Top;
            Properties.Settings.Default.Left = this.Left;

            // Ugly but because WPF Settings can not store arrays. 
            // Each array is turned into a formatted string that can be read back using Parse()
            Properties.Settings.Default.LowerEdgesCW = String.Join(";", lowerEdgeCW.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.UpperEdgesCW = String.Join(";", upperEdgeCW.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.RefLevelsCW = String.Join(";", refLevelCW.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.RefLevelsCWZ = String.Join(";", refLevelCWZ.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.PwrLevelsCW = String.Join(";", pwrLevelCW.Select(i => i.ToString()).ToArray());

            Properties.Settings.Default.LowerEdgesSSB = String.Join(";", lowerEdgeSSB.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.UpperEdgesSSB = String.Join(";", upperEdgeSSB.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.RefLevelsSSB = String.Join(";", refLevelSSB.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.RefLevelsSSBZ = String.Join(";", refLevelSSBZ.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.PwrLevelsSSB = String.Join(";", pwrLevelSSB.Select(i => i.ToString()).ToArray());

            Properties.Settings.Default.LowerEdgesDigital = String.Join(";", lowerEdgeDigital.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.UpperEdgesDigital = String.Join(";", upperEdgeDigital.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.RefLevelsDigital = String.Join(";", refLevelDigital.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.RefLevelsDigitalZ = String.Join(";", refLevelDigitalZ.Select(i => i.ToString()).ToArray());
            Properties.Settings.Default.PwrLevelsDigital = String.Join(";", pwrLevelDigital.Select(i => i.ToString()).ToArray());

            Properties.Settings.Default.COMport = ComPort;
            Properties.Settings.Default.Barefoot = Barefoot;

            Properties.Settings.Default.Save();
        }

        private void OnZoomButton(object sender, RoutedEventArgs e)
        {
            // Only do act if we have received information from N1MM
            if (RadioInfoReceived)
            {
                currentLowerEdge = currentFrequency - ZoomRange / 2;
                currentUpperEdge = currentLowerEdge + ZoomRange;

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

                // Set zoomed mode and color buttons accordingly
                Zoomed = true;
                ZoomButton.Background = ActiveColor;
                ZoomButton.BorderBrush = ActiveColor;
                BandModeButton.Background = PassiveColor;
                BandModeButton.BorderBrush = PassiveColor;

                // Disable text boxes for entry in Zoomed mode
                LowerEdgeTextbox.IsEnabled = false;
                UpperEdgeTextbox.IsEnabled = false;

                // Update radio and and UI 
                UpdateRadioEdges(currentLowerEdge, currentUpperEdge, RadioEdgeSet[currentMHz]);
                UpdateRadioReflevel(currentRefLevel);
            }
        }

        // On arrow key modification of slider
        private void OnRefSliderKey(object sender, KeyEventArgs e)
        {
            UpdateRefSlider();
        }

        private void ToggleBarefoot(object sender, MouseButtonEventArgs e)
        {
            if (!RadioInfoReceived) // Do not react until we received radio info
                return;

            Barefoot = !Barefoot;

            UpdateRadioPwrlevel(currentPwrLevel);
        }

        // On mouse modification of slider
        private void OnRefSliderMouseClick(object sender, MouseButtonEventArgs e)
        {
            UpdateRefSlider();
        }

        // Update ref level on slider action
        void UpdateRefSlider()
        {
            currentRefLevel = (int)(RefLevelSlider.Value + 0.0f);

            UpdateRadioReflevel(currentRefLevel);

            if (RadioInfoReceived) // Only remember value if we are in a known state
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
        }

        // on key movement of power slider
        private void OnPwrSliderKey(object sender, KeyEventArgs e)
        {
            UpdatePwrSlider();
        }

        // on mouse movement of power slider
        private void OnPwrSliderMouseClick(object sender, MouseButtonEventArgs e)
        {
            UpdatePwrSlider();
        }

        // Update pwr level on slider action
        void UpdatePwrSlider()
        {
            currentPwrLevel = (int)(PwrLevelSlider.Value + 0.0f);
            UpdateRadioPwrlevel(currentPwrLevel);

            if (currentMHz != 0)
                switch (currentMode)
                {
                    case "CW":
                        pwrLevelCW[bandIndex[currentMHz]] = currentPwrLevel;
                        break;
                    case "SSB":
                        pwrLevelSSB[bandIndex[currentMHz]] = currentPwrLevel;
                        break;
                    default:
                        pwrLevelDigital[bandIndex[currentMHz]] = currentPwrLevel;
                        break;
                }
        }

        // Update radio with new waterfall edges
        void UpdateRadioEdges(int lower_edge, int upper_edge, int ICOMedgeSegment)
        {
            // Compose CI-V command to set waterfall edges
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

            // Update UI if present (this function may be called before main window is created)
            if (LowerEdgeTextbox != null)
            {
                LowerEdgeTextbox.Text = lower_edge.ToString();
                UpperEdgeTextbox.Text = upper_edge.ToString();
            }

            // Update radio if we are not in debug mode
            if (!NoRadio)
            {
                port.Write(CIVSetFixedMode, 0, CIVSetFixedMode.Length); // Set fixed mode
                port.Write(CIVSetEdgeSet, 0, CIVSetEdgeSet.Length); // set edge set EdgeSet
                port.Write(CIVSetEdges, 0, CIVSetEdges.Length); // set edge set EdgeSet
            }
        }

        // Update radio with new REF level
        void UpdateRadioReflevel(int ref_level)
        {
            int absRefLevel = (ref_level >= 0) ? ref_level : -ref_level;

            CIVSetRefLevel[7] = (byte)((absRefLevel / 10) * 16 + absRefLevel % 10);
            CIVSetRefLevel[9] = (ref_level >= 0) ? (byte)0 : (byte)1;

            // Update UI if present (this function may be called before main window is created)
            if (RefLevelLabel != null)
            {
                RefLevelSlider.Value = ref_level;
                RefLevelLabel.Content = string.Format("Ref: {0:+#;-#;0}dB", ref_level);
            }

            // Update radio if we are not debugging
            if (!NoRadio)
                port.Write(CIVSetRefLevel, 0, CIVSetRefLevel.Length); // set edge set EdgeSet
        }

        // Update radio with new PWR level
        void UpdateRadioPwrlevel(int pwr_level)
        {
            int usedPower;

            // Update UI if present (this function may be called before main window is created)
            if (PwrLevelLabel != null)
            {
                if (Barefoot)
                {
                    PwrLevelSlider.IsEnabled = false;
                    PwrLevelLabel.Foreground = BarefootColor;
                    PwrLevelLabel.FontWeight = FontWeights.Bold;
                    usedPower = 255;
                    PwrLevelSlider.Value = 100;
                    PwrLevelLabel.Content = "Pwr:100%";
                }
                else
                {
                    PwrLevelSlider.IsEnabled = true;
                    PwrLevelLabel.Foreground = ExciterColor;
                    PwrLevelLabel.FontWeight = FontWeights.Normal;
                    usedPower = (int)((255.0f * pwr_level) / 100.0f + 0.99f); // Weird ICOM mapping of percent to binary
                    PwrLevelSlider.Value = pwr_level;
                    PwrLevelLabel.Content = string.Format("Pwr:{0,3}%", pwr_level);
                }

                CIVSetPwrLevel[6] = (byte)((usedPower / 100) % 10);
                CIVSetPwrLevel[7] = (byte)((((usedPower / 10) % 10) << 4) + (usedPower % 10));

                // Update radio if present
                if (!NoRadio)
                    port.Write(CIVSetPwrLevel, 0, CIVSetPwrLevel.Length); // set power level 
            }
        }
    }
}
