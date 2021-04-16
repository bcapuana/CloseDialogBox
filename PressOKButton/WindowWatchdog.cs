using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PressOKButton
{

    /// <summary>
    /// Event arguments for window opening.
    /// </summary>
    public class WindowOpenedEventArgs : EventArgs
    {
        public Window Window { get; set; }
        public WindowOpenedEventArgs(Window w) { Window = w; }
    }

    /// <summary>
    /// Event arguments for window closing.
    /// </summary>
    public class WindowClosedEventArgs : EventArgs
    {
        public Window Window { get; set; }
        public WindowClosedEventArgs(Window w) { Window = w; }
    }


    /// <summary>
    /// Window watchdog class
    /// </summary>
    public class WindowWatchdog
    {

        #region Fields


        private readonly string m_processName;
        private Thread m_thread;
        private readonly List<Window> m_openWindows = new List<Window>();
        private volatile bool m_continue = true;


        #endregion


        #region Constructor

        /// <summary>
        /// Creates a new instance of WindowWatchdog, watching the specified executable. 
        /// </summary>
        /// <param name="processName">The name of the process without the extension.</param>
        public WindowWatchdog(string processName)
        {
            m_processName = processName;
        }

        #endregion


        #region Methods

        /// <summary>
        /// Starts watching the process.
        /// </summary>
        public void Start()
        {
            m_thread = new Thread(() =>
            {
                WatchExe();
            });
            m_thread.IsBackground = true;
            m_thread.Start();
        }

        /// <summary>
        /// Watches the process for changes.
        /// </summary>
        private void WatchExe()
        {
            while (m_continue)
            {
                List<Window> currentWindows = GetCurrentWindows();
                CheckNewWindows(currentWindows);
                CheckClosedWindows(currentWindows);
                Thread.Sleep(100);
            }
        }

        
        /// <summary>
        /// Gets the windows that are currently open.
        /// </summary>
        /// <returns></returns>
        private List<Window> GetCurrentWindows()
        {
            List<Window> currentWindows = new List<Window>();
            Process[] procs = Process.GetProcessesByName(m_processName);

            if (procs.Length > 0)
            {
                Process p = procs.First();
                List<IntPtr> handles = EnumerateProcessWindowHandles(p.Id).ToList();
                foreach (IntPtr h in handles)
                    currentWindows.Add(new Window(h));
            }
            return currentWindows;
        }

        /// <summary>
        /// Checks for new windows
        /// </summary>
        /// <param name="currentWindows"></param>
        private void CheckNewWindows(List<Window> currentWindows)
        {
            // since this is running in a thread, lock the list so it can't be edited at the same time.
            lock (m_openWindows)
            {
                // loop through the current windows
                foreach (Window w in currentWindows)
                {
                    // does the window exist in the openwindows list?
                    if (m_openWindows.Find(ow => ow.Hwnd == w.Hwnd) == null)
                    {
                        // no
                        w.GetChildWindows();
                        m_openWindows.Add(w);
                        WindowOpened?.Invoke(this, new WindowOpenedEventArgs(w));
                    }
                }
            }
        }

        /// <summary>
        /// Checks for closed windows
        /// </summary>
        /// <param name="currentWindows"></param>
        private void CheckClosedWindows(List<Window> currentWindows)
        {
            // since this is running in a thread, lock the list so it can't be edited at the same time.
            lock (m_openWindows)
            {
                // find all of the windows that are not in the current windows list
                List<Window> closedWindows = m_openWindows.Where(w => !currentWindows.Any(w2 => w2.Hwnd == w.Hwnd)).ToList();

                // remove them from the open windows list
                m_openWindows.RemoveAll(w => closedWindows.Any(w2 => w.Hwnd == w2.Hwnd));

                // rais the window closed event.
                foreach (Window w in closedWindows)
                    WindowClosed?.Invoke(this, new WindowClosedEventArgs(w));
            }
        }

        /// <summary>
        /// Waits for the thread to exit.
        /// </summary>
        public void WaitForExit()
        {
            m_thread.Join();
        }

        /// <summary>
        /// Instructs the watchdog to stop
        /// </summary>
        public void Stop()
        {
            m_continue = false;
        }

        /// <summary>
        /// Enumerates all process windows
        /// </summary>
        /// <param name="processId"></param>
        /// <returns></returns>
        static IEnumerable<IntPtr> EnumerateProcessWindowHandles(int processId)
        {
            var handles = new List<IntPtr>();

            foreach (ProcessThread thread in Process.GetProcessById(processId).Threads)
                User32.EnumThreadWindows(thread.Id,
                    (hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);

            return handles;
        }

        #endregion

        #region Events

        public delegate void WindowOpenedEventDelegate(object sender, WindowOpenedEventArgs e);
        public delegate void WindowClosedEventDelegate(object sender, WindowClosedEventArgs e);
        public event WindowOpenedEventDelegate WindowOpened;
        public event WindowClosedEventDelegate WindowClosed;

        #endregion


    }
}
