using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GersangStation.Modules {

    public struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public int hwndActive;
        public int hwndFocus;
        public int hwndCapture;
        public int hwndMenuOwner;
        public int hwndMoveSize;
        public int hwndCaret;
    }

    public class ClipMouse
    {

        private static CancellationTokenSource tokenSource = new CancellationTokenSource();
        private static Thread thread1 = new Thread(() => main_thread(tokenSource.Token));
        private static IntPtr firstGameHandle = IntPtr.Zero;
        public static NotifyIcon? icon = null;

        // write는 UI 스레드만 하기 떄문에 큰 동시성 문제는 없음.
        public static bool isOnlyFirstClip = false;

        private const int WindowTitleMaxLength = 50; // Length window titles get truncated to
        private const int ValidateHandleThreshold = 10; // How often the user selected window handle gets validate
        private const int ClippingRefreshInterval = 100; // How often the clipped area is refreshed in milliseconds
        private const int HotKeyId = 33333;

        #region EnumerationsAndFlags
        private enum GetWindowLongIndex : int
        {
            GWL_WNDPROC = -4, GWL_HINSTANCE = -6, GWL_HWNDPARENT = -8, GWL_STYLE = -16, GWL_EXSTYLE = -20, GWL_USERDATA = -21, GWL_ID = -12
        }

        [Flags]
        private enum WindowStyles : int
        {
            WS_OVERLAPPED = 0x00000000, WS_POPUP = -2147483648, WS_CHILD = 0x40000000, WS_MINIMIZE = 0x20000000,
            WS_VISIBLE = 0x10000000, WS_DISABLED = 0x08000000, WS_CLIPSIBLINGS = 0x04000000, WS_CLIPCHILDREN = 0x02000000,
            WS_MAXIMIZE = 0x01000000, WS_CAPTION = 0x00C00000, WS_BORDER = 0x00800000, WS_DLGFRAME = 0x00400000,
            WS_VSCROLL = 0x00200000, WS_HSCROLL = 0x00100000, WS_SYSMENU = 0x00080000, WS_THICKFRAME = 0x00040000,
            WS_GROUP = 0x00020000, WS_TABSTOP = 0x00010000, WS_MINIMIZEBOX = 0x00020000, WS_MAXIMIZEBOX = 0x00010000
        }

        private enum SystemMetric : int
        {
            SM_CXBORDER = 5, SM_CYBORDER = 6, SM_CXSIZEFRAME = 32, SM_CYSIZEFRAME = 33, SM_CYCAPTION = 4, SM_CXFIXEDFRAME = 7, SM_CYFIXEDFRAME = 8
        }
        #endregion

        public static int GetHotKeyId()
        {
            return HotKeyId;
        }

        public static bool RegisterHotKey(IntPtr hWnd, Keys key)
        {
            //Register hotkey
            return RegisterHotKey(hWnd, GetHotKeyId(), 0 /*Prevent duplicated alarm*/, (int)key);
        }

        public static bool RegisterHotKey(IntPtr hWnd, string keyConfigVal) {
            if(keyConfigVal.Contains(',')) {
                string[] comb = keyConfigVal.Split(','); // ex: "Ctrl,122"
                string modStr = comb[0];
                int mod = 0;
                int key = int.Parse(comb[1]);
                if(modStr == "Ctrl") mod = 0x0002;
                else if(modStr == "Alt") mod = 0x0001;
                else if(modStr == "Shift") mod = 0x0004;

                return RegisterHotKey(hWnd, GetHotKeyId(), mod | 0x4000, key);
            } else {
                return RegisterHotKey(hWnd, (Keys)int.Parse(keyConfigVal));
            }
        }

        public static bool UnregisterHotKey(IntPtr hWnd)
        {
            return UnregisterHotKey(hWnd, GetHotKeyId());
        }

        public static bool isRunning()
        {
            return thread1.IsAlive;
        }

        //Run thread for mouse clip.
        //Output: True (SUCCEEDED)
        //        False (FAILED)
        public static bool Run()
        {
            Trace.WriteLine("Try to run clipMouse");
            if (isRunning())
            {
                Trace.WriteLine("Thread is already running");
                return false; //thread is already running
            }

            tokenSource = new CancellationTokenSource();
            thread1 = new Thread(() => main_thread(tokenSource.Token));

            thread1.Start();
            Trace.WriteLine("Thread Started");

            if (icon != null)
            {
                icon.Visible = true;
                icon.BalloonTipTitle = "향상된 마우스 가두기 ON";
                icon.BalloonTipText = "Alt 버튼을 누르면 일시적으로 빠져나올 수 있습니다.";
                icon.ShowBalloonTip(3000);
            }

            return true;
        }

        public static bool Stop(bool isFormClosed)
        {
            Trace.WriteLine("Try to stop clipMouse");
            if (!isRunning())
            {
                Trace.WriteLine("Thread is already stopped");
                return false;
            }

            tokenSource.Cancel();
            thread1.Join();
            tokenSource.Dispose();
            Trace.WriteLine("Thread Stopped");

            if (icon != null && !isFormClosed)
            {
                icon.Visible = true;
                icon.BalloonTipTitle = "향상된 마우스 가두기 OFF";
                icon.BalloonTipText = "F11 버튼을 눌러 다시 활성화 할 수 있습니다.";
                icon.ShowBalloonTip(3000);
            }

            return true;
        }

        private static void main_thread(CancellationToken token)
        {
            Trace.WriteLine("ClipMouse main started");
            bool isClipping = false; // 거상이 아닌 다른 프로세스의 마우스 가두기를 방해하는 것을 막기 위한 flag
            bool selectedWindowHadFocus = false;
            int validateHandleCount = 0;
            int escapeCount = 0;

            while (!token.IsCancellationRequested)
            {
                //Trace.WriteLine("ClipMouse main running");

                validateHandleCount++;

                IntPtr currentGameHandle = IntPtr.Zero;

                //Get windows handle for Gersang
                List<IntPtr> gameHandles = GetAllGameHandles();

                //Check current foreground
                bool hasFirstGameHandle = false;
                IntPtr foregroundWindow = GetForegroundWindow();
                foreach (IntPtr gameHandle in gameHandles) {
                    if (foregroundWindow == gameHandle) currentGameHandle = foregroundWindow;
                    if (firstGameHandle == gameHandle) hasFirstGameHandle = true;
                }

                // 고정했던 핸들이 현재 거상 핸들 목록에 없으면 초기화
                if(hasFirstGameHandle == false) firstGameHandle = IntPtr.Zero;

                if (validateHandleCount > ValidateHandleThreshold)
                {
                    validateHandleCount = 0;
                }

                if (currentGameHandle == IntPtr.Zero)
                { //Current foreground is not Gersang.
                    if(isClipping) {
                        ClipCursor(IntPtr.Zero); //Clear clip cursor
                        isClipping = false;
                    }
                    Thread.Sleep(ClippingRefreshInterval); //Wait next thread interval
                    continue;
                }

                if(isOnlyFirstClip) {
                    if(firstGameHandle == IntPtr.Zero) firstGameHandle = currentGameHandle;
                    else if(firstGameHandle != currentGameHandle) {
                        if(isClipping) {
                            ClipCursor(IntPtr.Zero); //Clear clip cursor
                            isClipping = false;
                        }
                        Thread.Sleep(ClippingRefreshInterval);
                        continue;
                    }
                }

                Rectangle windowArea = new Rectangle();
                Rectangle windowArea_original = new Rectangle();
                Rectangle escapeArea = new Rectangle();

                //Get current cursor pointer
                POINT pt;
                GetCursorPos(out pt);

                //Make a gap for safety 
                int borderGap = 2, escapeGap = 3;
                windowArea = GetGameArea(currentGameHandle);

                windowArea_original.Left = windowArea.Left;
                windowArea_original.Top = windowArea.Top;
                windowArea_original.Bottom = windowArea.Bottom;
                windowArea_original.Right = windowArea.Right;

                windowArea.Left += borderGap;
                windowArea.Top += borderGap;
                windowArea.Bottom -= borderGap;
                windowArea.Right -= borderGap;

                escapeArea.Left = windowArea.Left + escapeGap;
                escapeArea.Top = windowArea.Top + escapeGap;
                escapeArea.Bottom = windowArea.Bottom - escapeGap;
                escapeArea.Right = windowArea.Right - escapeGap;

                //Trace.WriteLine(windowArea_original);
                //Trace.WriteLine(escapeArea);
                //Trace.WriteLine(escapeCount);

                if (!escapeArea.IsPointInRectangle(pt))
                {
                    // Alt 키를 누르고 있으면서 비활성화 단축키 기능을 활성화 한 경우
                    if(Control.ModifierKeys == Keys.Alt && bool.Parse(ConfigManager.getConfig("use_clip_disable_hotkey"))) {
                        escapeCount++;
                    }
                }
                else
                {
                    escapeCount = 0;
                }

                //Escape function
                if (escapeCount > 2)
                {
                    escapeCount = 0;
                    ClipCursor(IntPtr.Zero);
                    selectedWindowHadFocus = false;
                    Thread.Sleep(1000);
                }
                else if (windowArea_original.IsPointInRectangle(pt))
                {
                    if(ClipCursor(ref windowArea) == 0) {
                        throw new Win32Exception(
                            Marshal.GetLastWin32Error(),
                            string.Format("Clip cursor win32 error. windowArea {0:s}", windowArea.ToString()));
                    } else isClipping = true;

                    selectedWindowHadFocus = true;

                }
                else if (selectedWindowHadFocus)
                {
                    // If the window lost focus remove the clipping area.
                    // Usually the clipping gets removed by default if the window loses focus. 
                    ClipCursor(IntPtr.Zero);
                    selectedWindowHadFocus = false;
                }

                Thread.Sleep(ClippingRefreshInterval);
            }

            ClipCursor(IntPtr.Zero); //Clear clip cursor
        }

        /// <summary>
        /// Generate a list of all active window handles and optionally prints out their title texts in a numbered list.
        /// </summary>
        /// <param name="outputWindowNames">If true all window title texts are printed out.</param>
        /// <returns>Return a list all active window handles.</returns>
        public static List<IntPtr> GetAllGameHandles(bool outputWindowNames = true)
        {
            Process[] processList;
            List<IntPtr> windowHandles = new List<IntPtr>();

            // Get Gersang process only
            processList = Process.GetProcessesByName("Gersang");

            if (windowHandles == null)
            {
                windowHandles = new List<IntPtr>();
            }
            else
            {
                windowHandles.Clear();
            }

            foreach (Process process in processList)
            {
                if (!string.IsNullOrEmpty(process.MainWindowTitle))
                {
                    windowHandles.Add(process.MainWindowHandle);
                }
            }

            return windowHandles;
        }

        public static Rectangle GetGameArea(IntPtr window) 
        {
            Rectangle ClientRect = new Rectangle();
            Rectangle WindowRect = new Rectangle();

            GetWindowRect(window, ref WindowRect);
            GetClientRect(window, ref ClientRect);

            //Trace.WriteLine("WindowRect: " + WindowRect);
            //Trace.WriteLine("ClientRect: " + ClientRect);

            if (ClientRect.Bottom - ClientRect.Top == WindowRect.Bottom - WindowRect.Top) //No title, No border
                return WindowRect;
            else
            {
                int borderWidth = ((WindowRect.Right - WindowRect.Left) - (ClientRect.Right - ClientRect.Left)) / 2;

                Rectangle GameRect = new Rectangle();
                GameRect.Left = ((WindowRect.Left + WindowRect.Right)/2) - ((ClientRect.Right - ClientRect.Left)/2);
                GameRect.Right = GameRect.Left + (ClientRect.Right - ClientRect.Left);
                GameRect.Bottom = WindowRect.Bottom - borderWidth;
                GameRect.Top = GameRect.Bottom - (ClientRect.Bottom - ClientRect.Top);
                //Trace.WriteLine("GameRect: " + GameRect);

                return GameRect;
            }
        }

        #region DLLImports
        [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "GetForegroundWindow")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "GetWindowRect")]
        private static extern int GetWindowRect(IntPtr hwnd, ref Rectangle lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "GetClientRect")]
        private static extern int GetClientRect(IntPtr hwnd, ref Rectangle lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "ClipCursor")]
        private static extern int ClipCursor(ref Rectangle lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "ClipCursor")]
        private static extern int ClipCursor(IntPtr lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "GetSystemMetrics")]
        private static extern int GetSystemMetrics(SystemMetric index);

        [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "GetWindowLong")]
        private static extern WindowStyles GetWindowLong(IntPtr hwnd, GetWindowLongIndex index);

        [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "GetCursorPos")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        // DLL libraries used to manage hotkeys
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "GetWindowPlacement")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT windowPlacement);

        #endregion

        /// <summary>
        /// An implementation of the WINAPI RECT structure.
        /// </summary>
        public struct Rectangle
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            /// <summary>
            /// Generates a string containing all attributes of the rectangle.
            /// </summary>
            /// <returns>Returns a string containing all attributes of the rectangle.</returns>
            public override string ToString()
            {
                return string.Format("Left : {0:d}, Top : {1:d}, Right : {2:d}, Bottom : {3:d}", Left, Top, Right, Bottom);
            }

            public bool IsPointInRectangle(in POINT pt)
            {
                return pt.x >= Left && pt.x <= Right && pt.y <= Bottom && pt.y >= Top;
            }
        }

        public struct POINT
        {
            public int x;
            public int y;
        }

        public enum ShowWindowCommand : int
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_MAXIMIZE = 3,
            SW_SHOWMAXIMIZED = 3,
            SW_SHOWNOACTIVATE = 4,
            SW_SHOW = 5,
            SW_MINIMIZE = 6,
            SW_SHOWMINNOACTIVE = 7,
            SW_SHOWNA = 8,
            SW_RESTORE = 9
        }

        public struct WINDOWPLACEMENT
        {
            public int Length;
            public int Flag;
            public ShowWindowCommand ShowWindowCommand;
            public POINT MinimumPosition;
            public POINT MaximumPosition;
            public Rectangle NormalRectangle;
            public static WINDOWPLACEMENT Default
            {
                get
                {
                    WINDOWPLACEMENT windowPlacement = new WINDOWPLACEMENT();

                    windowPlacement.Length = Marshal.SizeOf(windowPlacement);

                    return windowPlacement;
                }
            }
        }
    }
}
