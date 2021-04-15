using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PressOKButton
{

    class OpenWindow
    {
        public IntPtr Hwnd { get; set; }
        public string Text { get; set; }
        public List<OpenWindow> Children { get; } = new List<OpenWindow>();
    }


    class WindowOpenedEventArgs : EventArgs
    {
        public OpenWindow Window { get; set; }
        public WindowOpenedEventArgs(OpenWindow w) { Window = w; }
    }

    class WindowClosedEventArgs : EventArgs
    {
        public OpenWindow Window { get; set; }
        public WindowClosedEventArgs(OpenWindow w) { Window = w; }
    }

    delegate void WindowOpenedEventDelegate(object sender, WindowOpenedEventArgs e);
    delegate void WindowClosedEventDelegate(object sender, WindowOpenedEventArgs e);
    class WindowWatchdog
    {

        delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn,
            IntPtr lParam);


        public event WindowOpenedEventDelegate WindowOpened;
        public event WindowClosedEventDelegate WindowClosed;

        private string m_processName;
        private int m_processID;
        private Thread m_thread;
        private List<OpenWindow> m_openWindows = new List<OpenWindow>();
        private bool m_continue = true;

        public WindowWatchdog(string processName)
        {
            m_processName = processName;
        }

        public void Start()
        {
            m_thread = new Thread(() =>
            {
                while (m_continue)
                {
                    List<OpenWindow> currentWindows = new List<OpenWindow>();
                    Process[] procs = Process.GetProcessesByName(m_processName);

                    if (procs.Length > 0)
                    {
                        Process p = procs.First();
                        List<IntPtr> handles = EnumerateProcessWindowHandles(p.Id).ToList();
                        foreach (IntPtr handle in handles)
                        {
                            if (currentWindows.Find(w => w.Hwnd == handle) == null)
                            {
                                currentWindows.Add(new OpenWindow() { Hwnd = handle });
                            }
                        }



                        lock (m_openWindows)
                        {
                            foreach (OpenWindow w in currentWindows)
                            {
                                if (m_openWindows.Find(ow => ow.Hwnd == w.Hwnd) == null)
                                {
                                    WindowOpened?.Invoke(this, new WindowOpenedEventArgs(w));
                                    m_openWindows.Add(w);
                                }
                            }

                            List<OpenWindow> closedWindows = m_openWindows.Where(w => !currentWindows.Any(w2 => w2.Hwnd == w.Hwnd)).ToList();
                            foreach (OpenWindow w in closedWindows)
                                WindowClosed?.Invoke(this, new WindowOpenedEventArgs(w));
                            m_openWindows.RemoveAll(w => closedWindows.Any(w2 => w.Hwnd == w2.Hwnd));

                        }
                        Thread.Sleep(100);
                    }
                }
            });
            m_thread.IsBackground = true;
            m_thread.Start();
        }

        public void Stop()
        {
            m_continue = false;
        }

        static IEnumerable<IntPtr> EnumerateProcessWindowHandles(int processId)
        {
            var handles = new List<IntPtr>();

            foreach (ProcessThread thread in Process.GetProcessById(processId).Threads)
                EnumThreadWindows(thread.Id,
                    (hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);

            return handles;
        }

    }







    class Program
    {



        static void Main(string[] args)
        {
            WindowWatchdog wd = new WindowWatchdog("RswModus111u");
            wd.WindowOpened += Wd_WindowOpened;
            wd.WindowClosed += Wd_WindowClosed;
            wd.Start();

            Console.ReadLine();
        }

        private static void Wd_WindowClosed(object sender, WindowOpenedEventArgs e)
        {
            Console.WriteLine($"{e.Window.Hwnd} closed");
        }

        const int WM_KEYDOWN = 0x0100;
        const int VK_RETURN = 0x0D;

        private static void Wd_WindowOpened(object sender, WindowOpenedEventArgs e)
        {
            StringBuilder sb = new StringBuilder(256);
            GetWindowText(e.Window.Hwnd, sb, 256);
            e.Window.Text = sb.ToString();
            Console.WriteLine($"{e.Window.Hwnd}, {e.Window.Text} opened");

            EnumChildWindows(e.Window.Hwnd, (hwnd, lparam) =>
            {
                GetWindowText(hwnd, sb, 256);
                e.Window.Children.Add(new OpenWindow() { Hwnd = hwnd, Text=sb.ToString()});
                return true;
            }, IntPtr.Zero);
            foreach(OpenWindow w in e.Window.Children)
            {
                Console.WriteLine($"\t{w.Hwnd},{w.Text}");
            }

            if (e.Window.Children.Find(cw => cw.Text == "ERROR") != null)
            {
                PostMessage(e.Window.Children.Find(cw=>cw.Text=="OK").Hwnd,WM_KEYDOWN, VK_RETURN, 1);
                
            }

        }

        private delegate bool EnumWindowProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("User32.Dll")]
        public static extern Int32 PostMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        [DllImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumChildWindows(IntPtr window, EnumWindowProc callback, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    }
}
