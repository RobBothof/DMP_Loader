using Mindmagma.Curses;
using System.IO.Ports;

namespace Loader
{
    public struct DrawInstructionFileInfo
    {
        public String filename;
        public String fileheader;
        public Int32 version;
        public Int64 count;
        public Int64 start;
        public byte size;
        public Int64 index;
        public Int64 sendIndex;
    }

    public enum CommandByte
    {
        Home = 0xF0,
        Draw = 0xF1,
        Reset = 0xF2,
        MapHeight = 0xF3,
        Stop = 0xF4,
        EOF = 0xF5,
        ClearHeight = 0xF6,
        Zero = 0xF7,
        ZUp = 0xF8,
        ZDown = 0xF9,
        SetPenUp = 0xFA,
        SetPenMin = 0xFB,
        SetPenMax = 0xFC,
        PenUpPlus = 0xE0,
        PenUpMinus = 0xE1,
        PenMinPlus = 0xE2,
        PenMinMinus = 0xE3,
        PenMaxPlus = 0xE4,
        PenMaxMinus = 0xE5,
        XUp = 0xE6,
        XDown = 0xE7,
        SetXStart = 0xE8,
        YUp = 0xE9,
        YDown = 0xEA,
        SetYStart = 0xEB,
        Store = 0xEC,
        Recall = 0xED,
        End = 0xEE
    }

    class Program
    {
        private DrawInstructionFileInfo _driFileInfo;
        public static Queue<String> SerialMonitor = new Queue<string>();
        private SerialPort _serialPort = new SerialPort();
        private byte[] _serialMessageData = new byte[0];
        private String[] status = { };

        private static IntPtr Screen;
        private static IntPtr MainWindow;
        private static IntPtr SerialMonitorWindow;
        private static IntPtr SerialWindowButton;
        private static IntPtr FileWindowButton;
        private static IntPtr RefreshButton;
        private static IntPtr FileLoadButton;
        private static IntPtr SerialOpenButton;
        private static IntPtr SerialCloseButton;

        private static IntPtr HomeButton;
        private static IntPtr ResetButton;
        private static IntPtr HeightButton;
        private static IntPtr ClearHeightButton;
        private static IntPtr StopButton;
        private static IntPtr PaintButton;
        private static IntPtr ZeroButton;
        private static IntPtr EndButton;

        private static IntPtr StoreButton;
        private static IntPtr RecallButton;
        
        private static IntPtr XUpButton;
        private static IntPtr XDownButton;
        private static IntPtr YUpButton;
        private static IntPtr YDownButton;
        private static IntPtr ZUpButton;
        private static IntPtr ZDownButton;
        
        private static IntPtr SetPenUpButton;
        private static IntPtr SetPenMinButton;
        private static IntPtr SetPenMaxButton;

        private static IntPtr SetXOffsetButton;
        private static IntPtr SetYOffsetButton;

        private static IntPtr StatusWindow;

        uint Color_FileWindowNormal;
        uint Color_FileWindowHot;
        uint Color_ButtonNormal;
        uint Color_ButtonHot;
        uint Color_ButtonRunning;
        uint Color_MainWindowNormal;
        uint Color_MainWindowAccent;
        uint Color_MainWindowDim;
        uint Color_MainWindowColor;

        int screen_width;
        int screen_height;
        int selectedObject = 0;
        int lastselectedObject = -1;
        int lastkey = 0;

        int progress =0;

        ScrollWindow fileWindow = new ScrollWindow(15, 28, 1, 5);
        ScrollWindow serialWindow = new ScrollWindow(15, 28, 3, 5);

        static int Main(string[] args)
        {
            return new Program().Run();
        }

        int Run()
        {
            //
            _driFileInfo.fileheader = "none";
            _driFileInfo.filename = "no file loaded";
            _driFileInfo.count = 0;
            _driFileInfo.sendIndex = 0;
            //

            Screen = NCurses.InitScreen();

            if (!NCurses.HasColors())
            {
                Console.WriteLine("Sorry, this application currently requires terminal colors.");
                NCurses.EndWin();
                if (_serialPort.IsOpen) _serialPort.Close();
                return -1;
            }

            InitGui();

            int key = 0;
            while ((key = NCurses.GetChar()) != 113)
            { // q to quit
                checkSerial();
                if (screen_height != NCurses.Lines || screen_width != NCurses.Columns)
                {
                    Resize();
                }
                Update(key);

                NCurses.Nap(10);
            }

            if (_serialPort.IsOpen) _serialPort.Close();
            NCurses.EndWin();
            return 1;
        }

        void RefreshFilesAndSerial()
        {
            String lastfile = fileWindow.getFullSelected();
            fileWindow.elements = Directory.GetFiles("/home/robber/drawings", "*.dri");
            fileWindow.reselect(lastfile);

            String lastport = serialWindow.getFullSelected();
            serialWindow.elements = SerialPort.GetPortNames();
            serialWindow.reselect(lastport);
        }

        void ConnectSerialPort(String portname)
        {
            if (!_serialPort.IsOpen)
            {
                _serialPort = new SerialPort();
                _serialPort.PortName = portname;
                _serialPort.BaudRate = 115200;
                _serialPort.Parity = Parity.None;
                _serialPort.StopBits = StopBits.One;
                _serialPort.DataBits = 8;
                _serialPort.Handshake = Handshake.None;
                _serialPort.RtsEnable = true;
                _serialPort.DtrEnable = true;
                try
                {
                    _serialPort.Open();
                    serialMonitorAdd($"Connecting to: {portname} at 115200");
                }
                catch
                {
                    serialMonitorAdd("Error opening serialPort.");
                }
            }
            else
            {
                serialMonitorAdd("SerialPort is already open.");
            }
        }

        void DisConnectSerialPort()
        {
            serialMonitorAdd($"Disconnected from: {_serialPort.PortName}");
            if (_serialPort.IsOpen) _serialPort.Close();
        }

        void serialMonitorAdd(string s)
        {
            SerialMonitor.Enqueue(s);
            while (SerialMonitor.Count > screen_height - 4)
            {
                SerialMonitor.Dequeue();
            }
            NCurses.ClearWindow(SerialMonitorWindow);
            NCurses.WindowAttributeSet(SerialMonitorWindow, Color_MainWindowDim | CursesAttribute.ALTCHARSET);
            NCurses.Box(SerialMonitorWindow, 'x', 'q');
            NCurses.WindowAttributeSet(SerialMonitorWindow, Color_MainWindowNormal);
            NCurses.MoveWindowAddString(SerialMonitorWindow, 0, 2, "[ SerialMonitor ]");
            for (int i = 0; i < Math.Min(screen_height - 4, SerialMonitor.Count); i++)
            {
                NCurses.MoveWindowAddString(SerialMonitorWindow, i + 1, 2, SerialMonitor.ToArray()[Math.Max(screen_height - 4, SerialMonitor.Count) - (screen_height - 4) + i]);
            }
            NCurses.WindowRefresh(SerialMonitorWindow);
            //redraw serial window
        }

        void SerialMonitorRedraw()
        {
            while (SerialMonitor.Count > screen_height - 4)
            {
                SerialMonitor.Dequeue();
            }
            NCurses.ClearWindow(SerialMonitorWindow);
            NCurses.WindowAttributeSet(SerialMonitorWindow, Color_MainWindowDim | CursesAttribute.ALTCHARSET);
            NCurses.Box(SerialMonitorWindow, 'x', 'q');
            NCurses.WindowAttributeSet(SerialMonitorWindow, Color_MainWindowNormal);
            NCurses.MoveWindowAddString(SerialMonitorWindow, 0, 2, "[ SerialMonitor ]");
            for (int i = 0; i < Math.Min(screen_height - 4, SerialMonitor.Count); i++)
            {
                NCurses.MoveWindowAddString(SerialMonitorWindow, i + 1, 2, SerialMonitor.ToArray()[Math.Max(screen_height - 4, SerialMonitor.Count) - (screen_height - 4) + i]);
            }
            NCurses.WindowRefresh(SerialMonitorWindow);
        }

        void checkSerial()
        {
            if (_serialPort.IsOpen)
            {
                int b = _serialPort.BytesToRead;
                if (b > 0)
                {
                    String s = _serialPort.ReadLine();
                    if (s.Length > 0)
                    {
                        switch (s[0])
                        {
                            case '@':
                                //we have a data request
                                Int64 index = Int64.Parse(s.Split('@', 3)[1]);
                                SendInstruction(index);
                                break;
                            case '$':
                                //we have a status update
                                status = s.Split('$');

                                NCurses.TouchWindow(StatusWindow);
                                DrawStatusWindow();
                                NCurses.WindowRefresh(StatusWindow);
                                NCurses.WindowRefresh(MainWindow);
                                break;
                            default:
                                serialMonitorAdd(s);
                                break;

                        }
                    }
                }
            }
        }

        void LoadFileData()
        {
            if (File.Exists(fileWindow.getFullSelected()))
            {
                _driFileInfo.filename = fileWindow.getFullSelected();
                using (FileStream fileStream = new FileStream(fileWindow.getFullSelected(), FileMode.Open))
                {
                    byte[] tempbuffer = new byte[42];
                    // Write the data to the file, byte by byte.
                    fileStream.Seek(0, SeekOrigin.Begin);
                    fileStream.Read(tempbuffer);

                    _driFileInfo.fileheader = System.Text.Encoding.UTF8.GetString(tempbuffer, 0, 20);
                    _driFileInfo.version = BitConverter.ToInt32(tempbuffer, 20);
                    _driFileInfo.count = BitConverter.ToInt32(tempbuffer, 24);
                    _driFileInfo.start = BitConverter.ToInt64(tempbuffer, 32);
                    _driFileInfo.size = tempbuffer[40];
                }
            }
            _driFileInfo.index = 0;
            _driFileInfo.sendIndex = 0;
        }

        void sendCommand(CommandByte command)
        {
            if (_serialPort.IsOpen)
            {
                byte[] tempbuffer = new byte[10];
                for (int i = 0; i < 10; i++)
                {
                    tempbuffer[i] = (byte)command;
                }
                _serialPort.Write(tempbuffer, 0, tempbuffer.Length);
            }
            else
            {
                serialMonitorAdd("SerialPort is disconnected.");

            }
        }

        void SendInstruction(Int64 index)
        {
            //open one drawinstruction, check it, send to serial
            //Keep Track and Display Progress

            if (File.Exists(fileWindow.getFullSelected()))
            {
                if (index < _driFileInfo.count)
                {
                    _driFileInfo.filename = fileWindow.getFullSelected();
                    byte[] tempbuffer = new byte[_driFileInfo.size];
                    using (FileStream fileStream = new FileStream(fileWindow.getFullSelected(), FileMode.Open))
                    {
                        fileStream.Seek(_driFileInfo.start + _driFileInfo.size * index, SeekOrigin.Begin);
                        fileStream.Read(tempbuffer);
                    }
                    // verify drawinstruction
                    bool msgOK = true;
                    int numbytes = 0;
                    int checksum = 0;
                    for (int i = 0; i < tempbuffer.Length; i++)
                    {
                        if (msgOK)
                        {
                            if (i < 10)
                            {
                                if (tempbuffer[i] != 0xFF)
                                {
                                    msgOK = false;
                                }
                            }
                            if (i == 10)
                            {
                                numbytes = tempbuffer[10];
                            }
                            if (i > 10 && i < 11 + numbytes)
                            {
                                checksum += tempbuffer[i];
                            }
                            if (i == 11 + numbytes)
                            {
                                if (checksum != BitConverter.ToInt32(tempbuffer, i)) msgOK = false;
                            }
                        }
                    }

                    if (msgOK)
                    {
                        // Console.WriteLine($"file checksum {checksum} is ok! sending instruction");
                        if (_serialPort.IsOpen)
                        {
                            _serialPort.Write(tempbuffer, 0, tempbuffer.Length);
                            _driFileInfo.sendIndex = index + 1;
                            DrawMainWindowFileInfo();
                            NCurses.WindowRefresh(MainWindow);
                        }
                        else
                        {
                            serialMonitorAdd("SerialPort is disconnected.");
                        }
                    }
                    else
                    {
                        serialMonitorAdd($"file checksum {checksum}is bad! possible file corruption, aborting..");
                    }
                }
                else
                {
                    if (_driFileInfo.count == 0)
                    {
                        serialMonitorAdd("No drawing file loaded.");
                    }
                    else
                    {
                        serialMonitorAdd("EOF reached.");
                        sendCommand(CommandByte.EOF);
                    }
                }
            }
            else
            {
                serialMonitorAdd("No drawing file selected.");
            }
        }

        void InitGui()
        {
            NCurses.NoDelay(Screen, true);
            NCurses.NoEcho();
            NCurses.SetCursor(0);
            NCurses.Refresh();

            screen_width = NCurses.Columns;
            screen_height = NCurses.Lines;

            /////////////// COLORS /////////////////////////

            NCurses.StartColor();

            NCurses.InitPair(1, 6, 0);
            NCurses.InitPair(2, 8, 0);

            NCurses.InitPair(40, 7, 0);
            Color_MainWindowNormal = NCurses.ColorPair(40);

            NCurses.InitPair(41, 15, 0);
            Color_MainWindowAccent = NCurses.ColorPair(41);

            NCurses.InitPair(42, 8, 0);
            Color_MainWindowDim = NCurses.ColorPair(42);

            NCurses.InitPair(43, 10, 0);
            Color_MainWindowColor = NCurses.ColorPair(43);

            NCurses.InitPair(50, 15, 8);
            Color_FileWindowNormal = NCurses.ColorPair(50);

            NCurses.InitPair(51, 15, 1);
            Color_FileWindowHot = NCurses.ColorPair(51);

            NCurses.InitPair(52, 15, 8);
            Color_ButtonNormal = NCurses.ColorPair(52);

            NCurses.InitPair(53, 15, 1);
            Color_ButtonHot = NCurses.ColorPair(53);

            NCurses.InitPair(54, 15, 2);
            Color_ButtonRunning = NCurses.ColorPair(54);

            /////////////// GUI WINDOWS And Buttons/////////////////////////

            serialWindow.elements = new String[] { };
            serialWindow.colorNormal = Color_FileWindowNormal;
            serialWindow.colorHot = Color_FileWindowHot;
            serialWindow.id = 2;

            fileWindow.elements = new String[] { };
            fileWindow.colorNormal = Color_FileWindowNormal;
            fileWindow.colorHot = Color_FileWindowHot;
            fileWindow.id = 1;

            RefreshFilesAndSerial();

            fileWindow.Init(12, "no file selected", "no drawings found");
            serialWindow.Init(10, "no serial ports selected", "no serial ports found");
            fileWindow.Draw();
            serialWindow.Draw();

            MainWindow = NCurses.NewWindow(screen_height, screen_width, 0, 0);
            NCurses.WindowBackground(MainWindow, Color_MainWindowNormal);
            DrawMainWindow();
            DrawMainWindowFileInfo();
            NCurses.WindowRefresh(MainWindow);

            StatusWindow = NCurses.NewWindow(screen_height - 0, 52, 27, 0);
            NCurses.WindowBackground(StatusWindow, Color_MainWindowNormal);
            DrawStatusWindow();
            NCurses.WindowRefresh(StatusWindow);
            NCurses.WindowRefresh(MainWindow);

            SerialWindowButton = NCurses.NewWindow(1, serialWindow.width - 1, 3, 5);
            DrawButton(SerialWindowButton, Color_ButtonNormal, serialWindow.getSelected());

            FileWindowButton = NCurses.NewWindow(1, fileWindow.width - 1, 1, 5);
            DrawButton(FileWindowButton, Color_ButtonNormal, fileWindow.getSelected());

            RefreshButton = NCurses.NewWindow(1, 9, 5, 5);
            NCurses.WindowBackground(RefreshButton, Color_ButtonNormal);
            NCurses.WindowRefresh(RefreshButton);
            DrawButton(RefreshButton, Color_ButtonNormal, "Refresh");

            FileLoadButton = NCurses.NewWindow(1, 6, 1, fileWindow.width + 13);
            DrawButton(FileLoadButton, Color_ButtonNormal, "Load");

            SerialOpenButton = NCurses.NewWindow(1, 6, 3, fileWindow.width + 5);
            if (_serialPort.IsOpen)
            {
                DrawButton(SerialOpenButton, Color_ButtonRunning, "Open");
            }
            else
            {
                DrawButton(SerialOpenButton, Color_ButtonNormal, "Open");
            }

            SerialCloseButton = NCurses.NewWindow(1, 7, 3, fileWindow.width + 12);
            DrawButton(SerialCloseButton, Color_ButtonNormal, "Close");

            SerialMonitorWindow = NCurses.NewWindow(screen_height - 2, screen_width - 53, 1, 52);
            SerialMonitorRedraw();

            ////

            HomeButton = NCurses.NewWindow(1, 6, 13, 41);
            DrawButton(HomeButton, Color_ButtonNormal, "Home");

            ResetButton = NCurses.NewWindow(1, 7, 13, 33);
            DrawButton(ResetButton, Color_ButtonNormal, "Reset");

            ZeroButton = NCurses.NewWindow(1, 6, 13, 13);
            DrawButton(ZeroButton, Color_ButtonNormal, "Zero");

            PaintButton = NCurses.NewWindow(1, 7, 13, 5);
            DrawButton(PaintButton, Color_ButtonNormal, "Paint");

            StopButton = NCurses.NewWindow(1, 7, 15, 5);
            DrawButton(StopButton, Color_ButtonNormal, "Stop");

            EndButton = NCurses.NewWindow(1, 6, 15, 13);
            DrawButton(EndButton, Color_ButtonNormal, " End");

            HeightButton = NCurses.NewWindow(1, 5, 17, 42);
            DrawButton(HeightButton, Color_ButtonNormal, "Map");

            ClearHeightButton = NCurses.NewWindow(1, 7, 17, 34);
            DrawButton(ClearHeightButton, Color_ButtonNormal, "Clear");

            XDownButton = NCurses.NewWindow(1, 6, 19, 5);
            DrawButton(XDownButton, Color_ButtonNormal, "Left");

            XUpButton = NCurses.NewWindow(1, 7, 19, 12);
            DrawButton(XUpButton, Color_ButtonNormal, "Right");

            SetXOffsetButton = NCurses.NewWindow(1, 9, 19, 38);
            DrawButton(SetXOffsetButton, Color_ButtonNormal, "X Start");

            YDownButton = NCurses.NewWindow(1, 6, 21, 5);
            DrawButton(YDownButton, Color_ButtonNormal, "Back");

            YUpButton = NCurses.NewWindow(1, 5, 21, 12);
            DrawButton(YUpButton, Color_ButtonNormal, "Fwd");

            SetYOffsetButton = NCurses.NewWindow(1, 9, 21, 38);
            DrawButton(SetYOffsetButton, Color_ButtonNormal, "Y Start");

            ZUpButton = NCurses.NewWindow(1, 4, 23, 5);
            DrawButton(ZUpButton, Color_ButtonNormal, "Up");

            ZDownButton = NCurses.NewWindow(1, 6, 23, 10);
            DrawButton(ZDownButton, Color_ButtonNormal, "Down");

            SetPenUpButton = NCurses.NewWindow(1, 4, 23, 31);
            DrawButton(SetPenUpButton, Color_ButtonNormal, "Up");

            SetPenMinButton = NCurses.NewWindow(1, 5, 23, 36);
            DrawButton(SetPenMinButton, Color_ButtonNormal, "Min");

            SetPenMaxButton = NCurses.NewWindow(1, 5, 23, 42);
            DrawButton(SetPenMaxButton, Color_ButtonNormal, "Max");

            StoreButton = NCurses.NewWindow(1, 7, 25, 40);
            DrawButton(StoreButton, Color_ButtonNormal, "Store");

            RecallButton = NCurses.NewWindow(1, 8, 25, 31);
            DrawButton(RecallButton, Color_ButtonNormal, "Recall");

        }

        void Resize()
        {
            screen_height = NCurses.Lines;
            screen_width = NCurses.Columns;
            NCurses.Clear();
            NCurses.Refresh();
            NCurses.TouchWindow(MainWindow);
            NCurses.WindowRefresh(MainWindow);
            NCurses.TouchWindow(FileWindowButton);
            NCurses.WindowRefresh(FileWindowButton);
            NCurses.TouchWindow(FileLoadButton);
            NCurses.WindowRefresh(FileLoadButton);
            NCurses.TouchWindow(RefreshButton);
            NCurses.WindowRefresh(RefreshButton);

            NCurses.TouchWindow(SerialWindowButton);
            NCurses.WindowRefresh(SerialWindowButton);

            NCurses.TouchWindow(HomeButton);
            NCurses.WindowRefresh(HomeButton);
            NCurses.TouchWindow(PaintButton);
            NCurses.WindowRefresh(PaintButton);
            NCurses.TouchWindow(ResetButton);
            NCurses.WindowRefresh(ResetButton);
            NCurses.TouchWindow(StopButton);
            NCurses.WindowRefresh(StopButton);
            NCurses.TouchWindow(HeightButton);
            NCurses.WindowRefresh(HeightButton);
            NCurses.TouchWindow(ClearHeightButton);
            NCurses.WindowRefresh(ClearHeightButton);

            NCurses.TouchWindow(SerialOpenButton);
            NCurses.WindowRefresh(SerialOpenButton);
            NCurses.TouchWindow(SerialCloseButton);
            NCurses.WindowRefresh(SerialCloseButton);

            NCurses.TouchWindow(StatusWindow);
            NCurses.WindowRefresh(StatusWindow);

            SerialMonitorRedraw();

            if (fileWindow.selected)
            {
                fileWindow.TouchRefresh();
            }
            if (serialWindow.selected)
            {
                serialWindow.TouchRefresh();
            }
        }

        void Update(int c)
        {
            if (c != -1)
            {

                lastkey = c;
                if (!fileWindow.selected && !serialWindow.selected)
                {
                    if (c == 66)
                    { //keydown
                        switch (selectedObject)
                        {
                            case 0: selectedObject = 2; break;      // File Select
                            case 1: selectedObject = 4; break;      // Load
                            case 2: selectedObject = 5; break;      // Serial Select
                            case 3: selectedObject = 8; break;      // Open
                            case 4: selectedObject = 9; break;     // Close
                            case 5: selectedObject = 6; break;      // Refresh
                            case 6: selectedObject = 10; break;     // Paint
                            case 7: selectedObject = 11; break;     // Zero
                            case 8: selectedObject = 12; break;     // Reset
                            case 9: selectedObject = 13; break;    // Home
                            case 10: selectedObject = 14; break;     // Stop
                            case 11: selectedObject = 15; break;     // End
                            case 12: selectedObject = 22; break;    // Clear Height
                            case 13: selectedObject = 16; break;    // Map Height 
                            case 14: selectedObject = 17; break;    // X Up   
                            case 15: selectedObject = 18; break;    // X Down     
                            case 16: selectedObject = 19; break;    // Set XStart 
                            case 17: selectedObject = 20; break;    // Y Up
                            case 18: selectedObject = 21; break;    // Y Down
                            case 19: selectedObject = 24; break;    // Set YStart
                            case 20: selectedObject = 0; break;     // Z Up
                            case 21: selectedObject = 0; break;     // Z Down 
                            case 22: selectedObject = 25; break;     // Set Up
                            case 23: selectedObject = 26; break;     // Set Min
                            case 24: selectedObject = 26; break;     // Set Max
                            case 25: selectedObject = 1; break;     // Store
                            case 26: selectedObject = 1; break;     // Recall                            
                        }
                    }
                    if (c == 65)
                    { //keyup
                        switch (selectedObject)
                        {
                            case 0: selectedObject = 20; break;     // File Select
                            case 1: selectedObject = 26; break;     // Load
                            case 2: selectedObject = 0; break;      // Serial Select
                            case 3: selectedObject = 1; break;      // Open
                            case 4: selectedObject = 1; break;      // Close
                            case 5: selectedObject = 2; break;      // Refresh
                            case 6: selectedObject = 5; break;      // Paint
                            case 7: selectedObject = 5; break;      // Zero
                            case 8: selectedObject = 3; break;      // Reset
                            case 9: selectedObject = 4; break;     // Home
                            case 10: selectedObject = 6; break;      // Stop
                            case 11: selectedObject = 7; break;     // End                            
                            case 12: selectedObject = 8; break;     // Clear Height
                            case 13: selectedObject = 9; break;    // Map Height 
                            case 14: selectedObject = 10; break;     // X Up   
                            case 15: selectedObject = 11; break;     // X Down     
                            case 16: selectedObject = 13; break;    // Set XStart 
                            case 17: selectedObject = 14; break;    // Y Up
                            case 18: selectedObject = 15; break;    // Y Down
                            case 19: selectedObject = 16; break;    // Set YStart
                            case 20: selectedObject = 17; break;     // Z Up
                            case 21: selectedObject = 18; break;     // Z Down 
                            case 22: selectedObject = 12; break;     // Set Up
                            case 23: selectedObject = 19; break;     // Set Min
                            case 24: selectedObject = 19; break;     // Set Max
                            case 25: selectedObject = 22; break;     // Store
                            case 26: selectedObject = 24; break;     // Recall
                        }
                    }
                    if (c == 9 || c == 67)
                    { // TAB or right
                        selectedObject = (selectedObject + 1) % 27;
                    }
                    if (c == 90 || c == 68)
                    { //shifttab or left
                        selectedObject = (selectedObject + 26) % 27;
                    }

                    if (c == 61)
                    { // plus =
                        switch (selectedObject)
                        {
                            case 22:
                                {
                                    sendCommand(CommandByte.PenUpPlus);
                                    break;
                                }
                            case 23:
                                {
                                    sendCommand(CommandByte.PenMinPlus);
                                    break;
                                }
                            case 24:
                                {
                                    sendCommand(CommandByte.PenMaxPlus);
                                    break;
                                }
                        }
                    }

                    if (c == 45)
                    { // minus -
                        switch (selectedObject)
                        {
                            case 22:
                                {
                                    sendCommand(CommandByte.PenUpMinus);
                                    break;
                                }
                            case 23:
                                {
                                    sendCommand(CommandByte.PenMinMinus);
                                    break;
                                }
                            case 24:
                                {
                                    sendCommand(CommandByte.PenMaxMinus);
                                    break;
                                }
                        }
                    }

                    if (c == 10)
                    { //Return
                        switch (selectedObject)
                        {
                            case 0:
                                {
                                    if (fileWindow.elements.Length > 0)
                                    {
                                        fileWindow.selected = true;
                                        fileWindow.TouchRefresh();
                                    }
                                    break;
                                }
                            case 1:
                                {
                                    LoadFileData();
                                    DrawMainWindowFileInfo();
                                    NCurses.WindowRefresh(MainWindow);

                                    break;
                                }
                            case 2:
                                {
                                    if (serialWindow.elements.Length > 0)
                                    {
                                        serialWindow.selected = true;
                                        serialWindow.TouchRefresh();
                                    }
                                    break;
                                }
                            case 3:
                                {
                                    ConnectSerialPort(serialWindow.getFullSelected());
                                    break;

                                }
                            case 4:
                                {
                                    if (_serialPort.IsOpen)
                                    {
                                        DisConnectSerialPort();
                                        DrawButton(SerialOpenButton, Color_ButtonNormal, "Open"); break;
                                    }
                                    break;
                                }
                            case 5:
                                {
                                    RefreshFilesAndSerial();
                                    fileWindow.Resize(screen_height);
                                    fileWindow.Draw();
                                    serialWindow.Resize(screen_height);
                                    serialWindow.Draw();
                                    DrawButton(FileWindowButton, Color_ButtonNormal, fileWindow.getSelected());
                                    DrawButton(SerialWindowButton, Color_ButtonNormal, serialWindow.getSelected());

                                    // Redraw();
                                    break;
                                }
                            case 6:
                                {
                                    sendCommand(CommandByte.Draw);
                                    break;
                                }
                            case 7:
                                {
                                    sendCommand(CommandByte.Zero);
                                    break;
                                }
                            case 8:
                                {
                                    sendCommand(CommandByte.Reset);
                                    break;
                                }
                            case 9:
                                {
                                    sendCommand(CommandByte.Home);
                                    break;
                                }
                             case 10:
                                {
                                    sendCommand(CommandByte.Stop);
                                    break;
                                }
                             case 11:
                                {
                                    sendCommand(CommandByte.End);
                                    break;
                                }
                            case 12:
                                {
                                    sendCommand(CommandByte.ClearHeight);
                                    break;
                                }
                            case 13:
                                {
                                    sendCommand(CommandByte.MapHeight);
                                    break;
                                }
                             case 14:
                                {
                                    sendCommand(CommandByte.XUp);
                                    break;
                                }
                            case 15:
                                {
                                    sendCommand(CommandByte.XDown);
                                    break;
                                }
                            case 16:
                                {
                                    sendCommand(CommandByte.SetXStart);
                                    break;
                                }
                            case 17:
                                {
                                    sendCommand(CommandByte.YUp);
                                    break;
                                }
                            case 18:
                                {
                                    sendCommand(CommandByte.YDown);
                                    break;
                                }
                            case 19:
                                {
                                    sendCommand(CommandByte.SetYStart);
                                    break;
                                }
                            case 20:
                                {
                                    sendCommand(CommandByte.ZUp);
                                    break;
                                }
                            case 21:
                                {
                                    sendCommand(CommandByte.ZDown);
                                    break;
                                }
                            case 22:
                                {
                                    sendCommand(CommandByte.SetPenUp);
                                    break;
                                }
                            case 23:
                                {
                                    sendCommand(CommandByte.SetPenMin);
                                    break;
                                }
                            case 24:
                                {
                                    sendCommand(CommandByte.SetPenMax);
                                    break;
                                }
                            case 25:
                                {
                                    sendCommand(CommandByte.Recall);
                                    break;
                                }
                            case 26:
                                {
                                    sendCommand(CommandByte.Store);
                                    break;
                                }
                        }
                    }
                }
                else
                {
                    if (fileWindow.selected)
                    {
                        if (c == 10)
                        { //Return
                            fileWindow.selected = false;
                            fileWindow.select();
                            Redraw();
                        }
                        if (c == 9 || c == 66)
                        { // TAB or down
                            fileWindow.Next();
                            fileWindow.Draw();
                            fileWindow.TouchRefresh();
                        }
                        if (c == 90 || c == 65)
                        { //shifttab or up
                            fileWindow.Prev();
                            fileWindow.Draw();
                            fileWindow.TouchRefresh();
                        }

                    }

                    if (serialWindow.selected)
                    {
                        if (c == 10)
                        { //Return
                            serialWindow.select();
                            serialWindow.selected = false;
                            Redraw();
                        }
                        if (c == 9 || c == 66)
                        { // TAB or down
                            serialWindow.Next();
                            serialWindow.Draw();
                            serialWindow.TouchRefresh();
                        }
                        if (c == 90 || c == 65)
                        { //shifttab or up
                            serialWindow.Prev();
                            serialWindow.Draw();
                            serialWindow.TouchRefresh();
                        }
                    }
                }

            }
            else
            {
                if (lastkey == 27)
                {
                    //we have a proper escape
                    lastkey = 0;
                    if (fileWindow.selected)
                    {
                        fileWindow.selected = false;
                        Redraw();
                    }
                    if (serialWindow.selected)
                    {
                        serialWindow.selected = false;
                        Redraw();
                    }
                }
            }
            if (lastselectedObject != selectedObject)
            {
                switch (lastselectedObject)
                {
                    case 0: DrawButton(FileWindowButton, Color_ButtonNormal, fileWindow.getSelected()); break;
                    case 1: DrawButton(FileLoadButton, Color_ButtonNormal, "Load"); break;
                    case 2: DrawButton(SerialWindowButton, Color_ButtonNormal, serialWindow.getSelected()); break;
                    case 3:
                        {
                            if (_serialPort.IsOpen)
                            {
                                DrawButton(SerialOpenButton, Color_ButtonRunning, "Open"); break;
                            }
                            else
                            {
                                DrawButton(SerialOpenButton, Color_ButtonNormal, "Open"); break;
                            }
                        }
                    case 4: DrawButton(SerialCloseButton, Color_ButtonNormal, "Close"); break;
                    case 5: DrawButton(RefreshButton, Color_ButtonNormal, "Refresh"); break;
                    case 6: DrawButton(PaintButton, Color_ButtonNormal, "Paint"); break;
                    case 7: DrawButton(ZeroButton, Color_ButtonNormal, "Zero"); break;
                    case 8: DrawButton(ResetButton, Color_ButtonNormal, "Reset"); break;
                    case 9: DrawButton(HomeButton, Color_ButtonNormal, "Home"); break;
                    case 10: DrawButton(StopButton, Color_ButtonNormal, "Stop"); break;
                    case 11: DrawButton(EndButton, Color_ButtonNormal, " End"); break;
                    case 12: DrawButton(ClearHeightButton, Color_ButtonNormal, "Clear"); break;
                    case 13: DrawButton(HeightButton, Color_ButtonNormal, "Map"); break;

                    case 14: DrawButton(XDownButton, Color_ButtonNormal, "Left"); break;
                    case 15: DrawButton(XUpButton, Color_ButtonNormal, "Right"); break;
                    case 16: DrawButton(SetXOffsetButton, Color_ButtonNormal, "X Start"); break;

                    case 17: DrawButton(YDownButton, Color_ButtonNormal, "Back"); break;
                    case 18: DrawButton(YUpButton, Color_ButtonNormal, "Fwd"); break;
                    case 19: DrawButton(SetYOffsetButton, Color_ButtonNormal, "Y Start"); break;

                    case 20: DrawButton(ZUpButton, Color_ButtonNormal, "Up"); break;
                    case 21: DrawButton(ZDownButton, Color_ButtonNormal, "Down"); break;
                    case 22: DrawButton(SetPenUpButton, Color_ButtonNormal, "Up"); break;
                    case 23: DrawButton(SetPenMinButton, Color_ButtonNormal, "Min"); break;
                    case 24: DrawButton(SetPenMaxButton, Color_ButtonNormal, "Max"); break;
                    case 25: DrawButton(RecallButton, Color_ButtonNormal, "Recall"); break;
                    case 26: DrawButton(StoreButton, Color_ButtonNormal, "Store"); break;
                }

                switch (selectedObject)
                {
                    case 0: DrawButton(FileWindowButton, Color_ButtonHot, fileWindow.getSelected()); break;
                    case 1: DrawButton(FileLoadButton, Color_ButtonHot, "Load"); break;
                    case 2: DrawButton(SerialWindowButton, Color_ButtonHot, serialWindow.getSelected()); break;
                    case 3: DrawButton(SerialOpenButton, Color_ButtonHot, "Open"); break;
                    case 4: DrawButton(SerialCloseButton, Color_ButtonHot, "Close"); break;
                    case 5: DrawButton(RefreshButton, Color_ButtonHot, "Refresh"); break;
                    case 6: DrawButton(PaintButton, Color_ButtonHot, "Paint"); break;
                    case 7: DrawButton(ZeroButton, Color_ButtonHot, "Zero"); break;
                    case 8: DrawButton(ResetButton, Color_ButtonHot, "Reset"); break;
                    case 9: DrawButton(HomeButton, Color_ButtonHot, "Home"); break;
                    case 10: DrawButton(StopButton, Color_ButtonHot, "Stop"); break;
                    case 11: DrawButton(EndButton, Color_ButtonHot, " End"); break;
                    case 12: DrawButton(ClearHeightButton, Color_ButtonHot, "Clear"); break;
                    case 13: DrawButton(HeightButton, Color_ButtonHot, "Map"); break;
                    case 14: DrawButton(XDownButton, Color_ButtonHot, "Left"); break;
                    case 15: DrawButton(XUpButton, Color_ButtonHot, "Right"); break;
                    case 16: DrawButton(SetXOffsetButton, Color_ButtonHot, "X Start"); break;

                    case 17: DrawButton(YDownButton, Color_ButtonHot, "Back"); break;
                    case 18: DrawButton(YUpButton, Color_ButtonHot, "Fwd"); break;
                    case 19: DrawButton(SetYOffsetButton, Color_ButtonHot, "Y Start"); break;

                    case 20: DrawButton(ZUpButton, Color_ButtonHot, "Up"); break;
                    case 21: DrawButton(ZDownButton, Color_ButtonHot, "Down"); break;
                    case 22: DrawButton(SetPenUpButton, Color_ButtonHot, "Up"); break;
                    case 23: DrawButton(SetPenMinButton, Color_ButtonHot, "Min"); break;
                    case 24: DrawButton(SetPenMaxButton, Color_ButtonHot, "Max"); break;
                    case 25: DrawButton(RecallButton, Color_ButtonHot, "Recall"); break;
                    case 26: DrawButton(StoreButton, Color_ButtonHot, "Store"); break;                    
                }
                lastselectedObject = selectedObject;
            }
        }

        void Redraw()
        {
            NCurses.TouchWindow(MainWindow);
            NCurses.WindowRefresh(MainWindow);

            NCurses.TouchWindow(RefreshButton);
            NCurses.WindowRefresh(RefreshButton);

            NCurses.TouchWindow(FileLoadButton);
            NCurses.WindowRefresh(FileLoadButton);

            NCurses.TouchWindow(SerialCloseButton);
            NCurses.WindowRefresh(SerialCloseButton);

            NCurses.TouchWindow(SerialOpenButton);
            NCurses.WindowRefresh(SerialOpenButton);

            NCurses.TouchWindow(FileWindowButton);
            NCurses.ClearWindow(FileWindowButton);
            NCurses.MoveWindowAddString(FileWindowButton, 0, 1, fileWindow.getSelected());
            NCurses.WindowRefresh(FileWindowButton);

            NCurses.TouchWindow(SerialWindowButton);
            NCurses.ClearWindow(SerialWindowButton);
            NCurses.MoveWindowAddString(SerialWindowButton, 0, 1, serialWindow.getSelected());
            NCurses.WindowRefresh(SerialWindowButton);

            NCurses.TouchWindow(SerialMonitorWindow);
            NCurses.WindowRefresh(SerialMonitorWindow);

            NCurses.TouchWindow(StatusWindow);
            NCurses.WindowRefresh(StatusWindow);

            NCurses.TouchWindow(HomeButton);
            NCurses.WindowRefresh(HomeButton);

            NCurses.TouchWindow(ResetButton);
            NCurses.WindowRefresh(ResetButton);

            NCurses.TouchWindow(ZeroButton);
            NCurses.WindowRefresh(ZeroButton);

            NCurses.TouchWindow(EndButton);
            NCurses.WindowRefresh(EndButton);

            NCurses.TouchWindow(StopButton);
            NCurses.WindowRefresh(StopButton);

            NCurses.TouchWindow(PaintButton);
            NCurses.WindowRefresh(PaintButton);

            NCurses.TouchWindow(HeightButton);
            NCurses.WindowRefresh(HeightButton);

            NCurses.TouchWindow(ClearHeightButton);
            NCurses.WindowRefresh(ClearHeightButton);

            NCurses.TouchWindow(XUpButton);
            NCurses.WindowRefresh(XUpButton);

            NCurses.TouchWindow(XDownButton);
            NCurses.WindowRefresh(XDownButton);

            NCurses.TouchWindow(YUpButton);
            NCurses.WindowRefresh(YUpButton);

            NCurses.TouchWindow(YDownButton);
            NCurses.WindowRefresh(YDownButton);

            NCurses.TouchWindow(ZUpButton);
            NCurses.WindowRefresh(ZUpButton);

            NCurses.TouchWindow(ZDownButton);
            NCurses.WindowRefresh(ZDownButton);

            NCurses.TouchWindow(SetXOffsetButton);
            NCurses.WindowRefresh(SetXOffsetButton);

            NCurses.TouchWindow(SetYOffsetButton);
            NCurses.WindowRefresh(SetYOffsetButton);

            NCurses.TouchWindow(SetPenUpButton);
            NCurses.WindowRefresh(SetPenUpButton);

            NCurses.TouchWindow(SetPenMinButton);
            NCurses.WindowRefresh(SetPenMinButton);

            NCurses.TouchWindow(SetPenMaxButton);
            NCurses.WindowRefresh(SetPenMaxButton);

            NCurses.TouchWindow(StoreButton);
            NCurses.WindowRefresh(StoreButton);

            NCurses.TouchWindow(RecallButton);
            NCurses.WindowRefresh(RecallButton);

        }

        void DrawButton(IntPtr win, uint color, string text)
        {
            NCurses.WindowBackground(win, color);
            NCurses.MoveWindowAddString(win, 0, 1, text);
            NCurses.WindowRefresh(win);
        }

        void DrawStatusWindow()
        {
            int top = 0;
            int left = 0;

            int l = status.Length;

            NCurses.WindowAttributeSet(StatusWindow, Color_MainWindowDim | CursesAttribute.ALTCHARSET);
            NCurses.MoveWindowAddString(StatusWindow, top + 1, left, "lqqqqqqqqqqqqqqqqqqqqqwqqqqqqqqqqqqqqwqqqqqqqqqqqqqk");
            NCurses.MoveWindowAddString(StatusWindow, top + 2, left, "x            x        x              x             x");
            NCurses.MoveWindowAddString(StatusWindow, top + 3, left, "x            x        x              x             x");
            NCurses.MoveWindowAddString(StatusWindow, top + 4, left, "x            x        x              x             x");
            NCurses.MoveWindowAddString(StatusWindow, top + 5, left, "x            x        x              x             x");
            NCurses.MoveWindowAddString(StatusWindow, top + 6, left, "x            x        x              x             x");
            NCurses.MoveWindowAddString(StatusWindow, top + 7, left, "mqqqqqqqqqqqqqqqqqqqqqvqqqqqqqqqqqqqqvqqqqqqqqqqqqqj");

            NCurses.WindowAttributeSet(StatusWindow, Color_MainWindowDim);
            NCurses.MoveWindowAddString(StatusWindow, top + 0, left + 2, "positions");
            NCurses.MoveWindowAddString(StatusWindow, top + 0, left + 15, "swiches");
            NCurses.MoveWindowAddString(StatusWindow, top + 0, left + 29, "offsets");
            NCurses.MoveWindowAddString(StatusWindow, top + 0, left + 44, "others");

            NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 1, " 1");
            NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 1, " 2");
            NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 1, " 3");
            NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 1, " 4");
            NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 1, " 5");

            NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 15, " P");
            NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 15, "Y1");
            NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 15, "Y2");
            NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 15, " X");
            NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 15, " Z");

            NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 24, "X-ST");
            NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 24, "Y-ST");
            NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 24, "Z-Up");
            NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 24, "Z-Min");
            NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 24, "Z-Max");

            NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 39, "24v");
            NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 39, "line");
            NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 39, "OD-Z");
            NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 39, "OD-R");

            if (_driFileInfo.count > 0) {
                if (l > 23) {
                    progress = (Int32.Parse(status[23]) * 100) / ((int)_driFileInfo.count-1);
                } else {
                    progress = 0;
                }
                NCurses.MoveWindowAddString(MainWindow, 11, 41, (progress.ToString().PadLeft(5, ' ') + " %"));
            } else {
                NCurses.MoveWindowAddString(MainWindow, 11, 45,"0 %");
            }
            // NCurses.MoveWindowAddString(StatusWindow, top + 8 , left + 1, "Z-UP");
            // NCurses.MoveWindowAddString(StatusWindow, top + 9 , left + 1, "Z-Min");
            // NCurses.MoveWindowAddString(StatusWindow, top + 10, left + 1, "Z-Max");

            NCurses.WindowAttributeSet(StatusWindow, Color_MainWindowNormal);
            if (l > 1) NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 4, status[2].PadLeft(8, ' '));
            if (l > 2) NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 4, status[3].PadLeft(8, ' '));
            if (l > 3) NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 4, status[4].PadLeft(8, ' '));
            if (l > 5) NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 4, status[5].PadLeft(8, ' '));
            if (l > 6) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 4, status[6].PadLeft(8, ' '));

            // if (l > 7) NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 30, status[7].PadLeft(5, ' '));
            // if (l > 8) NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 30, status[8].PadLeft(5, ' '));
            // if (l > 9) NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 30, status[9].PadLeft(5, ' '));
            // if (l > 10) NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 30, status[10].PadLeft(5, ' '));
            // if (l > 11) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 30, status[11].PadLeft(5, ' '));

            if (l > 12) NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 42, (status[12] + " mA").PadLeft(8));

            if (l > 13) NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 18, status[13]);
            if (l > 14) NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 18, status[14]);
            if (l > 15) NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 18 + 2, status[15]);
            if (l > 16) NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 18, status[16]);
            if (l > 17) NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 18 + 2, status[17]);
            if (l > 18) NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 18, status[18]);
            if (l > 19) NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 18 + 2, status[19]);
            if (l > 20) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 18, status[20]);
            if (l > 21) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 18 + 2, status[21]);
            if (l > 22)
            {
                int functioncode = int.Parse(status[22]);
                if (functioncode == 0) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 39, "Idle".PadLeft(11, ' '));
                if (functioncode == 1) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 39, "Waiting".PadLeft(11, ' '));
                if (functioncode == 2) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 39, "Moving".PadLeft(11, ' '));
                if (functioncode == 3) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 39, "Drawing".PadLeft(11, ' '));
                if (functioncode == 4) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 39, "Homing".PadLeft(11, ' '));
                if (functioncode == 5) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 39, "Mapping".PadLeft(11, ' '));
            }
            if (l > 23) NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 43, status[23].PadLeft(7, ' '));
            if (l > 24) NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 43, status[24].PadLeft(7, ' '));
            if (l > 25) NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 43, status[25].PadLeft(7, ' '));

            // if (l > 26) NCurses.MoveWindowAddString(StatusWindow, top + 8 , left + 7, status[26].PadLeft(6, ' '));
            // if (l > 27) NCurses.MoveWindowAddString(StatusWindow, top + 9 , left + 7, status[27].PadLeft(6, ' '));
            // if (l > 28) NCurses.MoveWindowAddString(StatusWindow, top + 10, left + 7, status[28].PadLeft(6, ' '));

            if (l > 26) NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 29, status[26].PadLeft(7, ' '));
            if (l > 27) NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 29, status[27].PadLeft(7, ' '));
            if (l > 28) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 29, status[28].PadLeft(7, ' '));
            if (l > 29) NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 29, status[29].PadLeft(7, ' '));
            if (l > 30) NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 29, status[30].PadLeft(7, ' '));
        }
        void DrawMainWindow()
        {
            int top = 1;
            int left = 0;

            NCurses.WindowAttributeSet(MainWindow, Color_MainWindowDim | CursesAttribute.ALTCHARSET);
            NCurses.MoveWindowAddString(MainWindow, top + 0, left, " lq>                             qqqqqq>        >qqk");
            NCurses.MoveWindowAddString(MainWindow, top + 1, left, " x                                                 x");
            NCurses.MoveWindowAddString(MainWindow, top + 2, left, " tq>                             qqq>           >qqu");
            NCurses.MoveWindowAddString(MainWindow, top + 3, left, " x                                                 x");
            NCurses.MoveWindowAddString(MainWindow, top + 4, left, " mqq                                               x");
            NCurses.MoveWindowAddString(MainWindow, top + 5, left, "                                                   x");
            NCurses.MoveWindowAddString(MainWindow, top + 6, left, "                                               <qqqu");
            NCurses.MoveWindowAddString(MainWindow, top + 7, left, "                                               <qqqu");
            NCurses.MoveWindowAddString(MainWindow, top + 8, left, "                                               <qqqu");
            NCurses.MoveWindowAddString(MainWindow, top + 9, left, "                                               <qqqu");
            NCurses.MoveWindowAddString(MainWindow, top + 10, left, "                                                 <qu");
            NCurses.MoveWindowAddString(MainWindow, top + 11, left, "                                                   x");
            NCurses.MoveWindowAddString(MainWindow, top + 12, left, "                    >qqqqqqqqqq>                >qqu");
            NCurses.MoveWindowAddString(MainWindow, top + 13, left, "                                                   x");
            NCurses.MoveWindowAddString(MainWindow, top + 14, left, "                    >qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqu");
            NCurses.MoveWindowAddString(MainWindow, top + 15, left, "                                                   x");
            NCurses.MoveWindowAddString(MainWindow, top + 16, left, "                                                >qqu");
            NCurses.MoveWindowAddString(MainWindow, top + 17, left, "                                                   x");
            NCurses.MoveWindowAddString(MainWindow, top + 18, left, "                    qqqqqqqqqqqqqqqq>           >qqu");
            NCurses.MoveWindowAddString(MainWindow, top + 19, left, "                                                   x");
            NCurses.MoveWindowAddString(MainWindow, top + 20, left, "                  qqqqqqqqqqqqqqqqqq>           >qqu");
            NCurses.MoveWindowAddString(MainWindow, top + 21, left, "                                                   x");
            NCurses.MoveWindowAddString(MainWindow, top + 22, left, "                 qqqqqqqqqqqq>                  >qqu");
            NCurses.MoveWindowAddString(MainWindow, top + 23, left, " ^                                                 x");
            NCurses.MoveWindowAddString(MainWindow, top + 24, left, " mqqqqqqqqqqqqqqqqqqqqqqqqqqq<                  ,qqj");
 
            NCurses.WindowAttributeSet(MainWindow, Color_MainWindowDim);
            NCurses.MoveWindowAddString(MainWindow, top + 6, left + 1, "filename:");
            NCurses.MoveWindowAddString(MainWindow, top + 7, left + 1, "header:");
            NCurses.MoveWindowAddString(MainWindow, top + 8, left + 1, "drawcount:");
            NCurses.MoveWindowAddString(MainWindow, top + 9, left + 1, "last sent:");
            NCurses.MoveWindowAddString(MainWindow, top + 10, left + 1, "progress:");

            NCurses.MoveWindowAddString(MainWindow, top + 18, left + 21, "[paper offset]");
            NCurses.MoveWindowAddString(MainWindow, top + 20, left + 21, "[paper offset]");
            NCurses.MoveWindowAddString(MainWindow, top + 22, left + 18, "[tool set]");
            NCurses.WindowAttributeSet(MainWindow, Color_MainWindowNormal);
            NCurses.MoveWindowAddString(MainWindow, top + 18, left + 1, "X:");
            NCurses.MoveWindowAddString(MainWindow, top + 20, left + 1, "Y:");
            NCurses.MoveWindowAddString(MainWindow, top + 22, left + 1, "Z:");

        }

        void DrawMainWindowFileInfo()
        {
            int top = 0;
            int left = 0;
            NCurses.WindowAttributeSet(MainWindow, Color_MainWindowColor);
            string fn = _driFileInfo.filename.Split("/").Last();
            if (fn.Length > 37) fn = fn.Substring(0, 35) + ".RecallButtonButton.";
            NCurses.MoveWindowAddString(MainWindow, top + 7, left + 15, fn.PadLeft(31, ' '));
            string headerversion = _driFileInfo.fileheader + " | v" + _driFileInfo.version.ToString();
            NCurses.MoveWindowAddString(MainWindow, top + 8, left + 13, headerversion.PadLeft(33, ' '));
            NCurses.MoveWindowAddString(MainWindow, top + 9, left + 16, _driFileInfo.count.ToString().PadLeft(30, ' '));
            NCurses.MoveWindowAddString(MainWindow, top + 10, left + 30, _driFileInfo.sendIndex.ToString().PadLeft(16, ' '));
        }
    }
}

