using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms; // Required for NotifyIcon and Application
using System.Drawing; // Required for Icon and SystemIcons

namespace TaskbarHider
{
    class Program
    {
        // P/Invoke declarations
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        static extern uint SHAppBarMessage(uint dwMessage, [In, Out] ref APPBARDATA pData);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        const uint ABM_GETSTATE = 0x00000004;
        const uint ABM_SETSTATE = 0x0000000A;

        const int ABS_AUTOHIDE = 1;
        const int ABS_ALWAYSONTOP = 2;

        static List<TaskbarInfo> taskbars = new List<TaskbarInfo>();
        static DateTime? hideTime = null;
        static NotifyIcon notifyIcon;

        [STAThread]
        static void Main()
        {
            // Handle process exit to restore original taskbar settings
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            // Hide the console window at startup
            var handle = GetConsoleWindow();
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SW_HIDE);
            }

            // Enumerate all taskbars and save their original auto-hide states
            EnumWindows(EnumWindowsCallback, IntPtr.Zero);

            // At startup, disable auto-hide for all taskbars
            foreach (var taskbar in taskbars)
            {
                // Get original auto-hide state
                APPBARDATA abd = new APPBARDATA();
                abd.cbSize = (uint)Marshal.SizeOf(abd);
                abd.hWnd = taskbar.Handle;
                uint state = SHAppBarMessage(ABM_GETSTATE, ref abd);
                taskbar.OriginalAutoHideState = (int)state;

                // Disable auto-hide
                SetTaskbarAutoHide(taskbar.Handle, false);
            }

            // Set up system tray icon
            SetupTrayIcon();

            // Main loop
            while (true)
            {
                Application.DoEvents(); // Process Windows messages

                POINT cursorPos;
                if (GetCursorPos(out cursorPos))
                {
                    bool cursorOverAnyTaskbar = false;

                    foreach (var taskbar in taskbars)
                    {
                        if (IsCursorOverTaskbar(cursorPos, taskbar))
                        {
                            cursorOverAnyTaskbar = true;
                            break; // No need to check other taskbars
                        }
                    }

                    if (cursorOverAnyTaskbar)
                    {
                        // Cursor is over a taskbar, show all taskbars and cancel hide timer
                        foreach (var taskbar in taskbars)
                        {
                            if (!taskbar.IsVisible)
                            {
                                SetTaskbarAutoHide(taskbar.Handle, false);
                                taskbar.IsVisible = true;
                            }
                        }
                        hideTime = null; // Cancel any pending hide action
                    }
                    else
                    {
                        // Cursor is not over any taskbar
                        if (hideTime == null)
                        {
                            // Start the hide timer
                            hideTime = DateTime.Now.AddSeconds(5);
                        }
                        else if (DateTime.Now >= hideTime)
                        {
                            // Time to hide all taskbars
                            foreach (var taskbar in taskbars)
                            {
                                if (taskbar.IsVisible)
                                {
                                    SetTaskbarAutoHide(taskbar.Handle, true);
                                    taskbar.IsVisible = false;
                                }
                            }
                            hideTime = null;
                        }
                    }
                }

                Thread.Sleep(100); // Reduce CPU usage
            }
        }

        static void SetupTrayIcon()
        {
            notifyIcon = new NotifyIcon();

            // Option A: Use SystemIcons.Application (requires System.Drawing)
            // notifyIcon.Icon = SystemIcons.Application;

            // Option B: Use the application's associated icon
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            notifyIcon.Icon = Icon.ExtractAssociatedIcon(exePath);

            // Option C: Use a custom icon file (ensure the icon file is included in the project)
            // notifyIcon.Icon = new Icon("app.ico");

            notifyIcon.Visible = true;
            notifyIcon.Text = "Taskbar Hider";

            // Create context menu with an exit option
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem("Exit");
            exitMenuItem.Click += ExitMenuItem_Click;
            contextMenu.Items.Add(exitMenuItem);

            notifyIcon.ContextMenuStrip = contextMenu;
        }

        private static void ExitMenuItem_Click(object sender, EventArgs e)
        {
            notifyIcon.Visible = false;
            Application.Exit(); // This triggers OnProcessExit
            Environment.Exit(0);
        }

        static void OnProcessExit(object sender, EventArgs e)
        {
            // Restore original auto-hide states
            foreach (var taskbar in taskbars)
            {
                SetTaskbarAutoHide(taskbar.Handle, taskbar.OriginalAutoHideState == ABS_AUTOHIDE);
            }
        }

        static void SetTaskbarAutoHide(IntPtr taskbarHandle, bool enableAutoHide)
        {
            APPBARDATA abd = new APPBARDATA();
            abd.cbSize = (uint)Marshal.SizeOf(abd);
            abd.hWnd = taskbarHandle;
            abd.lParam = enableAutoHide ? ABS_AUTOHIDE : ABS_ALWAYSONTOP;

            SHAppBarMessage(ABM_SETSTATE, ref abd);
        }

        static bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam)
        {
            const int maxClassNameLength = 256;
            StringBuilder className = new StringBuilder(maxClassNameLength);
            GetClassName(hWnd, className, maxClassNameLength);

            if (className.ToString() == "Shell_TrayWnd" || className.ToString() == "Shell_SecondaryTrayWnd")
            {
                RECT rect;
                if (GetWindowRect(hWnd, out rect))
                {
                    var taskbarInfo = new TaskbarInfo
                    {
                        Handle = hWnd,
                        Rect = rect,
                        IsVisible = true,
                        OriginalAutoHideState = ABS_ALWAYSONTOP // Will be updated
                    };

                    taskbars.Add(taskbarInfo);
                }
            }

            return true; // Continue enumeration
        }

        static bool IsCursorOverTaskbar(POINT cursorPos, TaskbarInfo taskbar)
        {
            RECT rect = taskbar.Rect;
            return cursorPos.X >= rect.left && cursorPos.X <= rect.right &&
                   cursorPos.Y >= rect.top && cursorPos.Y <= rect.bottom;
        }

        // Supporting structs and classes
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        public class TaskbarInfo
        {
            public IntPtr Handle;
            public RECT Rect;
            public bool IsVisible;
            public int OriginalAutoHideState;
        }
    }
}
