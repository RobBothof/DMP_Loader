using System;
using System.Text.Encodings;
using Mindmagma.Curses;
using System.IO.Ports;

namespace Loader {
    public struct DrawInstructionFileInfo {
        public String filename;
        public String fileheader;
        public Int32 version;
        public Int64 count;
        public Int64 start;
        public byte size;
        public Int64 index;
        public Int64 sendIndex;
    }

    class Program {
        private DrawInstructionFileInfo _driFileInfo;
        public static Queue<String> SerialMonitor = new Queue<string>();
        private SerialPort _serialPort = new SerialPort();
        private byte[] _serialMessageData = new byte[0];
        private String _serialLastSendBytes = "";

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
        // private static IntPtr RunButton;
        // private static IntPtr StopButton;

        private static IntPtr HomeButton;
        private static IntPtr ResetButton;
        private static IntPtr HeightButton;
        private static IntPtr PauseButton;
        private static IntPtr BDrawButton;

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

        int machineState = 0;
        // bool running = false;

        ScrollWindow fileWindow = new ScrollWindow(15, 28, 1, 5);
        ScrollWindow serialWindow = new ScrollWindow(15, 28, 3, 5);

        static int Main(string[] args) {
            return new Program().Run();
        }

        int Run() {
            //
            _driFileInfo.fileheader = "none";
            _driFileInfo.filename = "no file loaded";
            _driFileInfo.count = 0;
            _driFileInfo.sendIndex = 0;
            //

            Screen = NCurses.InitScreen();

            if (!NCurses.HasColors()) {
                Console.WriteLine("Sorry, this application currently requires terminal colors.");
                NCurses.EndWin();
                if (_serialPort.IsOpen) _serialPort.Close();
                return -1;
            }

            InitGui();

            int key = 0;
            while ((key = NCurses.GetChar()) != 113) { // q to quit
                checkSerial();
                if (screen_height != NCurses.Lines || screen_width != NCurses.Columns) {
                    Resize();
                }
                Update(key);
                NCurses.Nap(10);
            }

            if (_serialPort.IsOpen) _serialPort.Close();
            NCurses.EndWin();
            return 1;
        }

        void RefreshFilesAndSerial() {
            String lastfile = fileWindow.getFullSelected();
            fileWindow.elements = Directory.GetFiles("/home/robber/drawings", "*.dri");
            fileWindow.reselect(lastfile);

            String lastport = serialWindow.getFullSelected();
            serialWindow.elements = SerialPort.GetPortNames();
            serialWindow.reselect(lastport);
        }

        void ConnectSerialPort(String portname) {
            if (!_serialPort.IsOpen) {
                _serialPort = new SerialPort();
                _serialPort.PortName = portname;
                _serialPort.BaudRate = 115200;
                _serialPort.Parity = Parity.None;
                _serialPort.StopBits = StopBits.One;
                _serialPort.DataBits = 8;
                _serialPort.Handshake = Handshake.None;
                _serialPort.RtsEnable = true;
                _serialPort.DtrEnable = true;
                try {
                    _serialPort.Open();
                    serialMonitorAdd($"Connecting to: {portname} at 115200");
                } catch {
                    // Console.WriteLine("Error opening serialPort.");
                    serialMonitorAdd("Error opening serialPort.");
                }
            } else {
                // Console.WriteLine("SerialPort is already open.");
                serialMonitorAdd("SerialPort is already open.");
            }
        }

        void DisConnectSerialPort() {
            serialMonitorAdd($"Disconnected from: {_serialPort.PortName}");
            if (_serialPort.IsOpen) _serialPort.Close();
        }

        void serialMonitorAdd(string s) {
            SerialMonitor.Enqueue(s);
            while (SerialMonitor.Count > screen_height - 4) {
                SerialMonitor.Dequeue();
            }
            NCurses.ClearWindow(SerialMonitorWindow);
            NCurses.WindowAttributeSet(SerialMonitorWindow, Color_MainWindowDim | CursesAttribute.ALTCHARSET);
            NCurses.Box(SerialMonitorWindow, 'x', 'q');
            NCurses.WindowAttributeSet(SerialMonitorWindow, Color_MainWindowNormal);
            NCurses.MoveWindowAddString(SerialMonitorWindow, 0, 2, "[ SerialMonitor ]");
            for (int i = 0; i < Math.Min(screen_height - 4, SerialMonitor.Count); i++) {
                NCurses.MoveWindowAddString(SerialMonitorWindow, i + 1, 2, SerialMonitor.ToArray()[Math.Max(screen_height - 4, SerialMonitor.Count) - (screen_height - 4) + i]);
            }
            NCurses.WindowRefresh(SerialMonitorWindow);
            //redraw serial window
        }

        void SerialMonitorRedraw() {
            while (SerialMonitor.Count > screen_height - 4) {
                SerialMonitor.Dequeue();
            }
            NCurses.ClearWindow(SerialMonitorWindow);
            NCurses.WindowAttributeSet(SerialMonitorWindow, Color_MainWindowDim | CursesAttribute.ALTCHARSET);
            NCurses.Box(SerialMonitorWindow, 'x', 'q');
            NCurses.WindowAttributeSet(SerialMonitorWindow, Color_MainWindowNormal);
            NCurses.MoveWindowAddString(SerialMonitorWindow, 0, 2, "[ SerialMonitor ]");
            for (int i = 0; i < Math.Min(screen_height - 4, SerialMonitor.Count); i++) {
                NCurses.MoveWindowAddString(SerialMonitorWindow, i + 1, 2, SerialMonitor.ToArray()[Math.Max(screen_height - 4, SerialMonitor.Count) - (screen_height - 4) + i]);
            }
            NCurses.WindowRefresh(SerialMonitorWindow);
        }

        void checkSerial() {
            if (_serialPort.IsOpen) {
                int b = _serialPort.BytesToRead;
                if (b > 0) {
                    String s = _serialPort.ReadLine();
                    if (s.Length > 0) {
                        switch (s[0]) {
                            case '@':
                                //we have a data request
                                Int64 index = Int64.Parse(s.Split('@', 3)[1]);
                                // if (running) {
                                    SendInstruction(index);
                                // }
                                break;
                            case '$':
                                //we have a status update
                                status = s.Split('$');

                                NCurses.TouchWindow(StatusWindow);
                                DrawStatusWindow();
                                NCurses.WindowRefresh(StatusWindow);
                                break;
                            default:
                                serialMonitorAdd(s);
                                break;

                        }
                    }
                }
            }
        }

        void LoadFileData() {
            if (File.Exists(fileWindow.getFullSelected())) {
                _driFileInfo.filename = fileWindow.getFullSelected();
                using (FileStream fileStream = new FileStream(fileWindow.getFullSelected(), FileMode.Open)) {
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

        void sendPauseCommand() {
            if (_serialPort.IsOpen) {
                byte[] tempbuffer = new byte[10];
                for (int i = 0; i < 10; i++) {
                    tempbuffer[i] = 0xF4;
                }
                _serialPort.Write(tempbuffer, 0, tempbuffer.Length);
            } else {
                serialMonitorAdd("SerialPort is disconnected.");

            }
        }

        void sendHeightMapCommand() {
            if (_serialPort.IsOpen) {
                byte[] tempbuffer = new byte[10];
                for (int i = 0; i < 10; i++) {
                    tempbuffer[i] = 0xF3;
                }
                _serialPort.Write(tempbuffer, 0, tempbuffer.Length);
            } else {
                serialMonitorAdd("SerialPort is disconnected.");

            }
        }

        void sendResetCommand() {
            if (_serialPort.IsOpen) {
                byte[] tempbuffer = new byte[10];
                for (int i = 0; i < 10; i++) {
                    tempbuffer[i] = 0xF2;
                }
                _serialPort.Write(tempbuffer, 0, tempbuffer.Length);
            } else {
                serialMonitorAdd("SerialPort is disconnected.");

            }
        }

        void sendDrawCommand() {
            if (_serialPort.IsOpen) {
                byte[] tempbuffer = new byte[10];
                for (int i = 0; i < 10; i++) {
                    tempbuffer[i] = 0xF1;
                }
                _serialPort.Write(tempbuffer, 0, tempbuffer.Length);
            } else {
                serialMonitorAdd("SerialPort is disconnected.");

            }
        }

        void sendHomeCommand() {
            if (_serialPort.IsOpen) {
                byte[] tempbuffer = new byte[10];
                for (int i = 0; i < 10; i++) {
                    tempbuffer[i] = 0xF0;
                }
                _serialPort.Write(tempbuffer, 0, tempbuffer.Length);
            } else {
                serialMonitorAdd("SerialPort is disconnected.");

            }
        
        }
        void SendInstruction(Int64 index) {
            //open one drawinstruction, check it, send to serial
            //Keep Track and Display Progress

            if (File.Exists(fileWindow.getFullSelected())) {
                if (index < _driFileInfo.count) {
                    _driFileInfo.filename = fileWindow.getFullSelected();
                    byte[] tempbuffer = new byte[_driFileInfo.size];
                    using (FileStream fileStream = new FileStream(fileWindow.getFullSelected(), FileMode.Open)) {
                        fileStream.Seek(_driFileInfo.start + _driFileInfo.size * index, SeekOrigin.Begin);
                        fileStream.Read(tempbuffer);
                    }
                    // verify drawinstruction
                    bool msgOK = true;
                    int numbytes = 0;
                    int checksum = 0;
                    for (int i = 0; i < tempbuffer.Length; i++) {
                        if (msgOK) {
                            if (i < 10) {
                                if (tempbuffer[i] != 0xFF) {
                                    msgOK = false;
                                }
                            }
                            if (i == 10) {
                                numbytes = tempbuffer[10];
                            }
                            if (i > 10 && i < 11 + numbytes) {
                                checksum += tempbuffer[i];
                            }
                            if (i == 11 + numbytes) {
                                if (checksum != BitConverter.ToInt32(tempbuffer, i)) msgOK = false;
                            }
                        }
                    }

                    if (msgOK) {
                        // Console.WriteLine($"file checksum {checksum} is ok! sending instruction");
                        if (_serialPort.IsOpen) {
                            _serialPort.Write(tempbuffer, 0, tempbuffer.Length);
                            // _serialLastSendBytes = BitConverter.ToString(tempbuffer);
                            // _driFileInfo.index=index;
                            _driFileInfo.sendIndex = index + 1;
                            DrawMainWindowFileInfo();
                            NCurses.WindowRefresh(MainWindow);
                        } else {
                            serialMonitorAdd("SerialPort is disconnected.");
                        }
                    } else {
                        serialMonitorAdd($"file checksum {checksum}is bad! possible file corruption, aborting..");
                    }
                }
            }


        }


        void InitGui() {
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

            //fileWindow.elements = new String[] { "file1.dri", "file2.dri", "file3.dri", "file4.dri", "file5.dri", "file6.dri", "file1.dri", "file2.dri", "file3.dri", "file4.dri", "file5.dri", "file6.dri" };
            fileWindow.Init(12, "no file selected", "no drawings found");
            serialWindow.Init(10, "no serial ports selected", "no serial ports found");
            fileWindow.Draw();
            serialWindow.Draw();


            MainWindow = NCurses.NewWindow(screen_height, screen_width, 0, 0);
            NCurses.WindowBackground(MainWindow, Color_MainWindowNormal);
            DrawMainWindow();
            DrawMainWindowFileInfo();
            NCurses.WindowRefresh(MainWindow);

            // StatusWindow = NCurses.NewWindow(screen_height, 20, 0, screen_width-20);
            StatusWindow = NCurses.NewWindow(screen_height - 0, 50, 20, 0);
            NCurses.WindowBackground(StatusWindow, Color_MainWindowNormal);
            DrawStatusWindow();
            // DrawMainWindowFileInfo();
            NCurses.WindowRefresh(StatusWindow);

            SerialWindowButton = NCurses.NewWindow(1, serialWindow.width - 1, 3, 5);
            DrawButton(SerialWindowButton, Color_ButtonNormal, serialWindow.getSelected());

            FileWindowButton = NCurses.NewWindow(1, fileWindow.width - 1, 1, 5);
            DrawButton(FileWindowButton, Color_ButtonNormal, fileWindow.getSelected());

            RefreshButton = NCurses.NewWindow(1, 9, 5, 5);
            NCurses.WindowBackground(RefreshButton, Color_ButtonNormal);
            NCurses.WindowRefresh(RefreshButton);
            DrawButton(RefreshButton, Color_ButtonNormal, "Refresh");

            FileLoadButton = NCurses.NewWindow(1, 6, 1, fileWindow.width + 12);
            DrawButton(FileLoadButton, Color_ButtonNormal, "Load");

            SerialOpenButton = NCurses.NewWindow(1, 6, 3, fileWindow.width + 5);
            if (_serialPort.IsOpen) {
                DrawButton(SerialOpenButton, Color_ButtonRunning, "Open");
            } else {
                DrawButton(SerialOpenButton, Color_ButtonNormal, "Open");
            }

            SerialCloseButton = NCurses.NewWindow(1, 7, 3, fileWindow.width + 12);
            DrawButton(SerialCloseButton, Color_ButtonNormal, "Close");

            // RunButton = NCurses.NewWindow(1, 5, 5, fileWindow.width + 6);
            // // if (running) {
            //     // DrawButton(RunButton, Color_ButtonRunning, "Run");
            // // } else {
            //     DrawButton(RunButton, Color_ButtonNormal, "Run");
            // // }

            // StopButton = NCurses.NewWindow(1, 6, 5, fileWindow.width + 12);
            // DrawButton(StopButton, Color_ButtonNormal, "Stop");

            SerialMonitorWindow = NCurses.NewWindow(screen_height - 2, screen_width - 53, 1, 52);
            SerialMonitorRedraw();

            ////

            HomeButton = NCurses.NewWindow(1, 6, 13, 40);
            DrawButton(HomeButton, Color_ButtonNormal, "Home");

            ResetButton = NCurses.NewWindow(1, 7, 13, 32);
            DrawButton(ResetButton, Color_ButtonNormal, "Reset");

            PauseButton = NCurses.NewWindow(1, 7, 13, 24);
            DrawButton(PauseButton, Color_ButtonNormal, "Pause");

            BDrawButton = NCurses.NewWindow(1, 6, 13, 17);
            DrawButton(BDrawButton, Color_ButtonNormal, "Draw");

            HeightButton = NCurses.NewWindow(1, 12, 16, 34);
            DrawButton(HeightButton, Color_ButtonNormal, "Map Height");

        }

        void Resize() {
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
            NCurses.TouchWindow(BDrawButton);
            NCurses.WindowRefresh(BDrawButton);
            NCurses.TouchWindow(ResetButton);
            NCurses.WindowRefresh(ResetButton);
            NCurses.TouchWindow(PauseButton);
            NCurses.WindowRefresh(PauseButton);
            NCurses.TouchWindow(HeightButton);
            NCurses.WindowRefresh(HeightButton);
            
            NCurses.TouchWindow(SerialOpenButton);
            NCurses.WindowRefresh(SerialOpenButton);

            // NCurses.MoveWindow(StatusWindow,0,screen_width-20);
            NCurses.TouchWindow(StatusWindow);
            NCurses.WindowRefresh(StatusWindow);

            SerialMonitorRedraw();

            if (fileWindow.selected) {
                fileWindow.TouchRefresh();
            }
            if (serialWindow.selected) {
                serialWindow.TouchRefresh();
            }
        }

        void Update(int c) {
            if (c != -1) {
                lastkey = c;
                if (!fileWindow.selected && !serialWindow.selected) {
                    if (c == 66) { //keydown
                        switch (selectedObject) {
                            case 0:  selectedObject = 2; break;
                            case 1:  selectedObject = 4; break;
                            case 2:  selectedObject = 5; break;
                            case 3:  selectedObject = 8; break;
                            case 4:  selectedObject = 9; break;
                            case 5:  selectedObject = 6; break;
                            case 6:  selectedObject = 0; break;
                            case 7:  selectedObject = 0; break;
                            case 8:  selectedObject = 10; break;
                            case 9:  selectedObject = 10; break;
                            case 10: selectedObject = 1; break;
                        }
                    }
                    if (c == 65) { //keyup
                        switch (selectedObject) {
                            case 0: selectedObject = 6; break;
                            case 1: selectedObject = 10; break;
                            case 2: selectedObject = 0; break;
                            case 3: selectedObject = 1; break;
                            case 4: selectedObject = 1; break;
                            case 5: selectedObject = 2; break;
                            case 6: selectedObject = 5; break;
                            case 7: selectedObject = 2; break;
                            case 8:  selectedObject = 3; break;
                            case 9:  selectedObject = 4; break;
                            case 10: selectedObject = 9; break;                            
                        }
                    }
                    if (c == 9 || c == 67) { // TAB or right
                        selectedObject = (selectedObject + 1) % 11;
                    }
                    if (c == 90 || c == 68) { //shifttab or left
                        selectedObject = (selectedObject + 10) % 11;
                    }

                    if (c == 10) { //Return
                        switch (selectedObject) {
                            case 0: {
                                    if (fileWindow.elements.Length > 0) {
                                        fileWindow.selected = true;
                                        fileWindow.TouchRefresh();
                                    }
                                    break;
                                }
                            case 1: {
                                    LoadFileData();
                                    DrawMainWindowFileInfo();
                                    NCurses.WindowRefresh(MainWindow);

                                    break;
                                }
                            case 2: {
                                    if (serialWindow.elements.Length > 0) {
                                        serialWindow.selected = true;
                                        serialWindow.TouchRefresh();
                                    }
                                    break;
                                }
                            case 3: {
                                    ConnectSerialPort(serialWindow.getFullSelected());
                                    break;

                                }
                            case 4: {
                                    if (_serialPort.IsOpen) {
                                        DisConnectSerialPort();
                                        DrawButton(SerialOpenButton, Color_ButtonNormal, "Open"); break;
                                    }
                                    break;
                                }
                            case 5: {
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
                            case 6: {
                                    sendDrawCommand();
                                    break;
                                }
                            case 7: {
                                    sendPauseCommand();
                                    break;
                                }
                            case 8: {
                                    sendResetCommand();
                                    break;
                                }
                            case 9: {
                                    sendHomeCommand();
                                    break;
                                }
                            case 10: {
                                    sendHeightMapCommand();
                                    break;
                                }
                        }
                    }
                } else {
                    if (fileWindow.selected) {
                        if (c == 10) { //Return
                            fileWindow.selected = false;
                            fileWindow.select();
                            Redraw();
                        }
                        if (c == 9 || c == 66) { // TAB or down
                            fileWindow.Next();
                            fileWindow.Draw();
                            fileWindow.TouchRefresh();
                        }
                        if (c == 90 || c == 65) { //shifttab or up
                            fileWindow.Prev();
                            fileWindow.Draw();
                            fileWindow.TouchRefresh();
                        }

                    }

                    if (serialWindow.selected) {
                        if (c == 10) { //Return
                            serialWindow.select();
                            serialWindow.selected = false;
                            Redraw();
                        }
                        if (c == 9 || c == 66) { // TAB or down
                            serialWindow.Next();
                            serialWindow.Draw();
                            serialWindow.TouchRefresh();
                        }
                        if (c == 90 || c == 65) { //shifttab or up
                            serialWindow.Prev();
                            serialWindow.Draw();
                            serialWindow.TouchRefresh();
                        }
                    }
                }

            } else {
                if (lastkey == 27) {
                    //we have a proper escape
                    lastkey = 0;
                    if (fileWindow.selected) {
                        fileWindow.selected = false;
                        Redraw();
                    }
                    if (serialWindow.selected) {
                        serialWindow.selected = false;
                        Redraw();
                    }
                }
            }
            if (lastselectedObject != selectedObject) {
                switch (lastselectedObject) {
                    case 0: DrawButton(FileWindowButton, Color_ButtonNormal, fileWindow.getSelected()); break;
                    case 1: DrawButton(FileLoadButton, Color_ButtonNormal, "Load"); break;
                    case 2: DrawButton(SerialWindowButton, Color_ButtonNormal, serialWindow.getSelected()); break;
                    case 3: {
                            if (_serialPort.IsOpen) {
                                DrawButton(SerialOpenButton, Color_ButtonRunning, "Open"); break;
                            } else {
                                DrawButton(SerialOpenButton, Color_ButtonNormal, "Open"); break;
                            }
                        }
                    case 4: DrawButton(SerialCloseButton, Color_ButtonNormal, "Close"); break;
                    case 5: DrawButton(RefreshButton, Color_ButtonNormal, "Refresh"); break;
                    case 6: DrawButton(BDrawButton, Color_ButtonNormal, "Draw"); break;
                    case 7: DrawButton(PauseButton, Color_ButtonNormal, "Pause"); break;
                    case 8: DrawButton(ResetButton, Color_ButtonNormal, "Reset"); break;
                    case 9: DrawButton(HomeButton, Color_ButtonNormal, "Home"); break;
                    case 10: DrawButton(HeightButton, Color_ButtonNormal, "Map Height"); break;
                }

                switch (selectedObject) {
                    case 0: DrawButton(FileWindowButton, Color_ButtonHot, fileWindow.getSelected()); break;
                    case 1: DrawButton(FileLoadButton, Color_ButtonHot, "Load"); break;
                    case 2: DrawButton(SerialWindowButton, Color_ButtonHot, serialWindow.getSelected()); break;
                    case 3: DrawButton(SerialOpenButton, Color_ButtonHot, "Open"); break;
                    case 4: DrawButton(SerialCloseButton, Color_ButtonHot, "Close"); break;
                    case 5: DrawButton(RefreshButton, Color_ButtonHot, "Refresh"); break;
                    case 6: DrawButton(BDrawButton, Color_ButtonHot, "Draw"); break;
                    case 7: DrawButton(PauseButton, Color_ButtonHot, "Pause"); break;
                    case 8: DrawButton(ResetButton, Color_ButtonHot, "Reset"); break;
                    case 9: DrawButton(HomeButton, Color_ButtonHot, "Home"); break;
                    case 10: DrawButton(HeightButton, Color_ButtonHot, "Map Height"); break;

                }
                lastselectedObject = selectedObject;
            }
        }

        void Redraw() {
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

            NCurses.TouchWindow(PauseButton);
            NCurses.WindowRefresh(PauseButton);

            NCurses.TouchWindow(BDrawButton);
            NCurses.WindowRefresh(BDrawButton);

            NCurses.TouchWindow(HeightButton);
            NCurses.WindowRefresh(HeightButton);

        }

        void DrawButton(IntPtr win, uint color, string text) {
            NCurses.WindowBackground(win, color);
            NCurses.MoveWindowAddString(win, 0, 1, text);
            NCurses.WindowRefresh(win);
        }

        void DrawStatusWindow() {
            int top = 0;
            int left = 0;

            int l = status.Length;

            NCurses.WindowAttributeSet(StatusWindow, Color_MainWindowDim);
            NCurses.MoveWindowAddString(StatusWindow, top, left + 5, "position");
            NCurses.MoveWindowAddString(StatusWindow, top, left + 17, "swich");
            NCurses.MoveWindowAddString(StatusWindow, top, left + 31, "load");

            NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 1, "M1:");
            NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 1, "M2:");
            NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 1, "M3:");
            NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 1, "M4:");
            NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 1, "M5:");

            NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 15, "X1:");
            NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 15, "X2:");
            NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 15, " Y:");
            NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 15, " Z:");

            NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 26, "M1:");
            NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 26, "M2:");
            NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 26, "M3:");
            NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 26, "M4:");
            NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 26, "M5:");

            NCurses.MoveWindowAddString(StatusWindow, top, left + 42, "other");
            NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 38, "24v:");

            NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 37, "line:");

            NCurses.WindowAttributeSet(StatusWindow, Color_MainWindowNormal);
            if (l > 1) NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 4, status[2].PadLeft(9, ' '));
            if (l > 2) NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 4, status[3].PadLeft(9, ' '));
            if (l > 3) NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 4, status[4].PadLeft(9, ' '));
            if (l > 5) NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 4, status[5].PadLeft(9, ' '));
            if (l > 6) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 4, status[6].PadLeft(9, ' '));

            if (l > 7) NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 30, status[7].PadLeft(5, ' '));
            if (l > 8) NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 30, status[8].PadLeft(5, ' '));
            if (l > 9) NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 30, status[9].PadLeft(5, ' '));
            if (l > 10) NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 30, status[10].PadLeft(5, ' '));
            if (l > 11) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 30, status[11].PadLeft(5, ' '));

            if (l > 12) NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 42, (status[12] + " mA").PadLeft(8));

            if (l > 13) NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 19, status[13]);
            if (l > 14) NCurses.MoveWindowAddString(StatusWindow, top + 2, left + 19 + 2, status[14]);
            if (l > 15) NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 19, status[15]);
            if (l > 16) NCurses.MoveWindowAddString(StatusWindow, top + 3, left + 19 + 2, status[16]);
            if (l > 17) NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 19, status[17]);
            if (l > 18) NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 19 + 2, status[18]);
            if (l > 19) NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 19, status[19]);
            if (l > 20) NCurses.MoveWindowAddString(StatusWindow, top + 5, left + 19 + 2, status[20]);

            if (l > 21) {
                int functioncode = int.Parse(status[21]);
                if (functioncode == 0) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 37, "Waiting".PadLeft(13, ' '));
                if (functioncode == 1) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 37, "Homing".PadLeft(13, ' '));
                if (functioncode == 2) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 37, "Moving".PadLeft(13, ' '));
                if (functioncode == 3) NCurses.MoveWindowAddString(StatusWindow, top + 6, left + 37, "Drawing".PadLeft(13, ' '));
            }
            if (l > 22) NCurses.MoveWindowAddString(StatusWindow, top + 4, left + 42, status[22].PadLeft(8, ' '));

        }
        void DrawMainWindow() {
            int top = 1;
            int left = 0;

            NCurses.WindowAttributeSet(MainWindow, Color_MainWindowDim | CursesAttribute.ALTCHARSET);
            NCurses.MoveWindowAddString(MainWindow, top + 0, left, " lq>                             qqqqq>        >qk");
            NCurses.MoveWindowAddString(MainWindow, top + 1, left, " x                                               x");
            NCurses.MoveWindowAddString(MainWindow, top + 2, left, " tq>                             qqq>           >u");
            NCurses.MoveWindowAddString(MainWindow, top + 3, left, " x                                               x");
            NCurses.MoveWindowAddString(MainWindow, top + 4, left, " mqq                                             x");
            NCurses.MoveWindowAddString(MainWindow, top + 5, left, "                                                 x");
            NCurses.MoveWindowAddString(MainWindow, top + 6, left, "                                              <qqu");
            NCurses.MoveWindowAddString(MainWindow, top + 7, left, "                                              <qqu");
            NCurses.MoveWindowAddString(MainWindow, top + 8, left, "                                              <qqu");
            NCurses.MoveWindowAddString(MainWindow, top + 9, left, "                                              <qqu");
            NCurses.MoveWindowAddString(MainWindow, top + 10, left, "                                                 x");
            NCurses.MoveWindowAddString(MainWindow, top + 11, left, "                                                 x");
            NCurses.MoveWindowAddString(MainWindow, top + 12, left, "                                               >qu");
            NCurses.MoveWindowAddString(MainWindow, top + 13, left, "                                                 x");
            NCurses.MoveWindowAddString(MainWindow, top + 14, left, "                                                 x");
            NCurses.MoveWindowAddString(MainWindow, top + 15, left, "                                               >qj");

            NCurses.WindowAttributeSet(MainWindow, Color_MainWindowNormal);
            NCurses.MoveWindowAddString(MainWindow, top + 6, left + 5, "filename:");
            NCurses.MoveWindowAddString(MainWindow, top + 7, left + 5, "header:");
            NCurses.MoveWindowAddString(MainWindow, top + 8, left + 5, "drawcount:");
            NCurses.MoveWindowAddString(MainWindow, top + 9, left + 5, "last instruction sent:");
        }

        void DrawMainWindowFileInfo() {
            int top = 0;
            int left = 0;
            NCurses.WindowAttributeSet(MainWindow, Color_MainWindowColor);
            string fn = _driFileInfo.filename.Split("/").Last();
            if (fn.Length > 37) fn = fn.Substring(0, 35) + "..";
            NCurses.MoveWindowAddString(MainWindow, top + 7, left + 15, fn.PadLeft(30, ' '));
            string headerversion = _driFileInfo.fileheader + " | v" + _driFileInfo.version.ToString();
            NCurses.MoveWindowAddString(MainWindow, top + 8, left + 13, headerversion.PadLeft(32, ' '));
            NCurses.MoveWindowAddString(MainWindow, top + 9, left + 16, _driFileInfo.count.ToString().PadLeft(29, ' '));
            NCurses.MoveWindowAddString(MainWindow, top + 10, left + 30, _driFileInfo.sendIndex.ToString().PadLeft(15, ' '));
        }
    }
}

