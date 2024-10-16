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

        // Constants
        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        const uint ABM_GETSTATE = 0x00000004;
        const uint ABM_SETSTATE = 0x0000000A;

        const int ABS_AUTOHIDE = 1;
        const int ABS_ALWAYSONTOP = 2;

        // Flag to control the exit of the main loop
        static bool isExiting = false;

        // Lists to keep track of taskbars and full-screen windows
        static List<TaskbarInfo> taskbars = new List<TaskbarInfo>();
        static List<FullScreenWindow> fullScreenWindows = new List<FullScreenWindow>();

        // Timers for hiding and showing taskbars
        static DateTime? hideTime = null;
        static DateTime? showTime = null; // For the 0.7-second delay

        // NotifyIcon for the system tray
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

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

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

                if (isExiting)
                {
                    break; // Exit the loop gracefully
                }

                POINT cursorPos;
                if (GetCursorPos(out cursorPos))
                {
                    bool cursorOverAnyTaskbar = false;

                    foreach (var taskbar in taskbars)
                    {
                        if (IsCursorOverTaskbar(cursorPos, taskbar))
                        {
                            cursorOverAnyTaskbar = true;
                            break;
                        }
                    }

                    if (cursorOverAnyTaskbar)
                    {
                        if (showTime == null)
                        {
                            showTime = DateTime.Now.AddSeconds(0.7);
                        }
                        if (DateTime.Now >= showTime)
                        {
                            // Cursor is over the taskbar edge, remove full-screen windows and disable auto-hide
                            foreach (var taskbar in taskbars)
                            {
                                if (!taskbar.IsVisible)
                                {
                                    SetTaskbarAutoHide(taskbar.Handle, false);
                                    taskbar.IsVisible = true;
                                }
                            }
                            // Close full-screen windows
                            CloseFullScreenWindows();

                            hideTime = null; // Cancel any pending hide action
                            showTime = null;
                        }
                    }
                    else
                    {
                        showTime = null; // Reset showTime when cursor leaves the edge

                        if (hideTime == null)
                        {
                            // Start the hide timer
                            hideTime = DateTime.Now.AddSeconds(5);
                        }
                        else if (DateTime.Now >= hideTime)
                        {
                            // Time to enable auto-hide and create full-screen windows
                            foreach (var taskbar in taskbars)
                            {
                                if (taskbar.IsVisible)
                                {
                                    SetTaskbarAutoHide(taskbar.Handle, true);
                                    taskbar.IsVisible = false;
                                }
                            }
                            // Create full-screen windows
                            CreateFullScreenWindows();

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

            // Use the application's associated icon
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            notifyIcon.Icon = Icon.ExtractAssociatedIcon(exePath);

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
            isExiting = true;
            Application.Exit(); // This triggers OnProcessExit
            // Removed Environment.Exit(0); to allow graceful exit
        }

        static void OnProcessExit(object sender, EventArgs e)
        {
            // Restore original auto-hide states
            foreach (var taskbar in taskbars)
            {
                SetTaskbarAutoHide(taskbar.Handle, taskbar.OriginalAutoHideState == ABS_AUTOHIDE);
                taskbar.IsVisible = true;
            }

            // Close full-screen windows
            CloseFullScreenWindows();
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
                    // Determine which screen the taskbar is on
                    Rectangle taskbarRectangle = new Rectangle(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
                    Screen taskbarScreen = Screen.FromRectangle(taskbarRectangle);

                    var taskbarInfo = new TaskbarInfo
                    {
                        Handle = hWnd,
                        Rect = rect,
                        IsVisible = true,
                        OriginalAutoHideState = ABS_ALWAYSONTOP, // Will be updated
                        Screen = taskbarScreen
                    };

                    taskbars.Add(taskbarInfo);
                }
            }

            return true; // Continue enumeration
        }

        static bool IsCursorOverTaskbar(POINT cursorPos, TaskbarInfo taskbar)
        {
            RECT rect = taskbar.Rect;
            Screen screen = taskbar.Screen;
            Rectangle screenBounds = screen.Bounds;

            // Determine the orientation of the taskbar
            bool isTop = rect.top == screenBounds.Top;
            bool isBottom = rect.bottom == screenBounds.Bottom;
            bool isLeft = rect.left == screenBounds.Left;
            bool isRight = rect.right == screenBounds.Right;

            // Check if the cursor is within the taskbar's bounds
            if (isTop || isBottom)
            {
                return cursorPos.Y >= rect.top && cursorPos.Y <= rect.bottom &&
                       cursorPos.X >= screenBounds.Left && cursorPos.X <= screenBounds.Right;
            }
            else if (isLeft || isRight)
            {
                return cursorPos.X >= rect.left && cursorPos.X <= rect.right &&
                       cursorPos.Y >= screenBounds.Top && cursorPos.Y <= screenBounds.Bottom;
            }

            return false;
        }

        static void CreateFullScreenWindows()
        {
            if (fullScreenWindows.Count > 0)
                return;

            // Enumerate monitors and create a full-screen window for each
            Screen[] screens = Screen.AllScreens;
            foreach (var screen in screens)
            {
                FullScreenWindow fsWindow = new FullScreenWindow(screen);
                fsWindow.Show();
                fullScreenWindows.Add(fsWindow);
            }
        }

        static void CloseFullScreenWindows()
        {
            foreach (var fsWindow in fullScreenWindows)
            {
                fsWindow.Close();
            }
            fullScreenWindows.Clear();
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
            public Screen Screen; // Reference to the screen where the taskbar is located
        }

        public class FullScreenWindow : Form
        {
            public FullScreenWindow(Screen screen)
            {
                this.FormBorderStyle = FormBorderStyle.None;
                this.ShowInTaskbar = false;
                this.StartPosition = FormStartPosition.Manual;
                this.Bounds = screen.Bounds;
                this.BackColor = Color.Black;
                this.TopMost = true;

                // Make the window fully opaque for debugging
                this.Opacity = 0.01;

                // Optionally, add a label for debugging
                Label debugLabel = new Label();
                debugLabel.Text = "Full-Screen Window Active";
                debugLabel.Font = new Font("Arial", 24, FontStyle.Bold);
                debugLabel.ForeColor = Color.White;
                debugLabel.BackColor = Color.Transparent;
                debugLabel.AutoSize = true;
                debugLabel.Location = new Point(50, 50); // Adjust position as needed
                this.Controls.Add(debugLabel);

                // Make the window click-through after debugging
                // To revert, set Opacity to 0.01 and re-enable WS_EX_TRANSPARENT

                // Uncomment the following lines after debugging to make the window click-through
                this.Opacity = 0.01; // Almost transparent
                int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
                exStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT;
                SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);

                // Force the window to refresh and show the label
                this.Refresh();
            }

            protected override bool ShowWithoutActivation => true;

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams baseParams = base.CreateParams;
                    baseParams.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                    return baseParams;
                }
            }

            private const int GWL_EXSTYLE = -20;
            private const int WS_EX_LAYERED = 0x00080000;
            private const int WS_EX_TRANSPARENT = 0x00000020;

            [DllImport("user32.dll", SetLastError = true)]
            static extern int GetWindowLong(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll")]
            static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                base.OnFormClosing(e);
            }
        }
    }
}
