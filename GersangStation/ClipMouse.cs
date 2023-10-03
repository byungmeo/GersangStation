﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Linq;

namespace GersangStation
{

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

        private const int WindowTitleMaxLength = 50; // Length window titles get truncated to
        private const int ValidateHandleThreshold = 10; // How often the user selected window handle gets validate
        private const int ClippingRefreshInterval = 100; // How often the clipped area is refreshed in milliseconds

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

        //Run thread for mouse clip.
        //Output: True (SUCCEEDED)
        //        False (FAILED)
        public static bool Run()
        {
            Trace.WriteLine("Try to run clipMouse");
            if (thread1.IsAlive == true) {
                Trace.WriteLine("Thread is already running");
                return false; //thread is already running
            } 

            tokenSource = new CancellationTokenSource();
            thread1 = new Thread(() => main_thread(tokenSource.Token));

            thread1.Start();
            Trace.WriteLine("Thread Started");

            return true;
        }

        public static bool Stop()
        {
            Trace.WriteLine("Try to stop clipMouse");
            if (thread1.IsAlive == false) {
                Trace.WriteLine("Thread is already stopped");
                return false;
            } 

            tokenSource.Cancel();
            thread1.Join();
            tokenSource.Dispose();
            Trace.WriteLine("Thread Stopped");

            return true;
        }

        private static void main_thread(CancellationToken token) 
        {
            Trace.WriteLine("ClipMouse main started");
            bool selectedWindowHadFocus = false;
            int validateHandleCount = 0;

            while (!token.IsCancellationRequested) 
            {
                Trace.WriteLine("ClipMouse main running");

                validateHandleCount++;

                IntPtr currentWindowsHandle = IntPtr.Zero;

                //Get windows handle for Gersang
                List<IntPtr> windowHandles = GetAllWindowHandles();

                //Check current foreground
                IntPtr foregroundWindow = GetForegroundWindow();
                foreach (IntPtr windowHandle in windowHandles) {
                    if (foregroundWindow == windowHandle) {
                        currentWindowsHandle = foregroundWindow;
                    }
                }

                if (validateHandleCount > ValidateHandleThreshold)
                {
                    validateHandleCount = 0;
                }

                if (currentWindowsHandle == IntPtr.Zero) { //Current foreground is not Gersang.
                    ClipCursor(IntPtr.Zero); //Clear clip cursor
                    Thread.Sleep(ClippingRefreshInterval); //Wait next thread interval
                    continue;
                }

                Rectangle windowBorderSize = new Rectangle();
                Rectangle windowArea = new Rectangle();
                Rectangle windowArea_original = new Rectangle();

                // Determine border sizes for the selected window
                windowBorderSize = GetWindowBorderSizes(currentWindowsHandle);

                //Get current cursor pointer
                POINT pt;
                GetCursorPos(out pt);

                //Get windows rectangle by handle
                if (GetWindowRect(currentWindowsHandle, ref windowArea) == 0)
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        string.Format("Get window rectangle win32 error. selectedWindowHandle {0:d}", currentWindowsHandle));
                }

                //Make a gap for safety 
                int borderGap = 2;
                windowArea.Left += windowBorderSize.Left;
                windowArea.Top += windowBorderSize.Top;
                windowArea.Bottom -= windowBorderSize.Bottom;
                windowArea.Right -= windowBorderSize.Right;

                windowArea_original.Left = windowArea.Left;
                windowArea_original.Top = windowArea.Top;
                windowArea_original.Bottom = windowArea.Bottom;
                windowArea_original.Right = windowArea.Right;

                windowArea.Left += borderGap;
                windowArea.Top += borderGap;
                windowArea.Bottom -= borderGap;
                windowArea.Right -= borderGap;

                if ((windowArea_original.IsPointInRectangle(pt)))
                {
                    if (ClipCursor(ref windowArea) == 0)
                    {
                        throw new Win32Exception(
                            Marshal.GetLastWin32Error(),
                            string.Format("Clip cursor win32 error. windowArea {0:s}", windowArea.ToString()));
                    }

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
        public static List<IntPtr> GetAllWindowHandles(bool outputWindowNames = true)
        {
            Process[] processList;
            List<IntPtr> windowHandles = new List<IntPtr>();
            int indexCounter;

            // Print out (almost) every window title and save their handle
            processList = Process.GetProcesses();
            indexCounter = 1;

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
                //if (verboseOutput)
                //{
                //    Console.WriteLine($"{process.ProcessName}: {process.MainWindowHandle}|{process.MainWindowTitle}");
                //}

                if (!string.IsNullOrEmpty(process.MainWindowTitle) && process.ProcessName.Contains("Gersang"))
                {
                    if (outputWindowNames)
                    {
                        string windowTitle = RemoveSpecialCharacters(process.MainWindowTitle);
                        Console.WriteLine(
                            "({0:d}) : {1:s}",
                            indexCounter,
                            windowTitle.Substring(0, Math.Min(windowTitle.Length, WindowTitleMaxLength)));
                    }

                    windowHandles.Add(process.MainWindowHandle);
                    indexCounter++;
                }
            }

            return windowHandles;
        }

        /// <summary>
        /// Removes all escape and other non standard characters from the string so it can be safely printed to the console.
        /// </summary>
        /// <param name="str">The string to be sanitized.</param>
        /// <returns>Return the sanitized string.</returns>
        public static string RemoveSpecialCharacters(string str)
        {
            return Regex.Replace(str, "[^a-zA-Z0-9_. -]+", string.Empty, RegexOptions.Compiled);
        }

        /// <summary>
        /// Gets the size in pixel of a window's border.
        /// </summary>
        /// <param name="window">The handle of the window.</param>
        /// <returns>Returns the border size in pixel.</returns>
        public static Rectangle GetWindowBorderSizes(IntPtr window)
        {
            Rectangle windowBorderSizes = new Rectangle();

            WindowStyles styles = GetWindowLong(window, GetWindowLongIndex.GWL_STYLE);

            // Window has title-bar
            if (styles.HasFlag(WindowStyles.WS_CAPTION))
            {
                windowBorderSizes.Top += GetSystemMetrics(SystemMetric.SM_CYCAPTION);
            }

            // Window has re-sizable borders
            if (styles.HasFlag(WindowStyles.WS_THICKFRAME))
            {
                windowBorderSizes.Left += GetSystemMetrics(SystemMetric.SM_CXSIZEFRAME);
                windowBorderSizes.Right += GetSystemMetrics(SystemMetric.SM_CXSIZEFRAME);
                windowBorderSizes.Top += GetSystemMetrics(SystemMetric.SM_CYSIZEFRAME);
                windowBorderSizes.Bottom += GetSystemMetrics(SystemMetric.SM_CYSIZEFRAME);
            }
            else if (styles.HasFlag(WindowStyles.WS_BORDER) || styles.HasFlag(WindowStyles.WS_CAPTION))
            {
                // Window has normal borders
                windowBorderSizes.Left += GetSystemMetrics(SystemMetric.SM_CXFIXEDFRAME);
                windowBorderSizes.Right += GetSystemMetrics(SystemMetric.SM_CXFIXEDFRAME);
                windowBorderSizes.Top += GetSystemMetrics(SystemMetric.SM_CYFIXEDFRAME);
                windowBorderSizes.Bottom += GetSystemMetrics(SystemMetric.SM_CYFIXEDFRAME);
            }

            return windowBorderSizes;
        }

        /// <summary>
        /// Used to retrieve the title text of a window.
        /// </summary>
        /// <param name="hwnd">The handle of the window.</param>
        /// <param name="maxStringLength">The maximum length of the title string returned. Longer titles are truncated.</param>
        /// <returns>Return the title text of the window.</returns>
        //private static string GetWindowText(IntPtr hwnd, int maxStringLength)
        //{
        //    StringBuilder stringBuilder = new StringBuilder(maxStringLength);
        //    if (UnmanagedGetWindowText(hwnd, stringBuilder, maxStringLength) == 0)
        //    {
        //        return null;
        //    }

        //    return stringBuilder.ToString();
        //}

        #region DLLImports
        [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "GetWindowText")]
        private static extern int UnmanagedGetWindowText(IntPtr hwnd, StringBuilder lpString, int maxCount);

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
            public Int32 x;
            public Int32 y;
        }

    }
}
