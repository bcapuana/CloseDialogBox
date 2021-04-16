using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PressOKButton
{

    /// <summary>
    /// Class for storing information about open windows
    /// </summary>
    public class Window
    {
        /// <summary>
        /// The window handle
        /// </summary>
        public IntPtr Hwnd { get; private set; }

        /// <summary>
        /// The window text
        /// </summary>
        public string Text { get; private set; }

        /// <summary>
        /// The child windows
        /// </summary>
        public List<Window> Children { get; } = new List<Window>();

        /// <summary>
        /// Creates a new instance of a window with the specified handle
        /// </summary>
        /// <param name="hWnd">The handle to the window.</param>
        public Window(IntPtr hWnd)
        {
            Hwnd = hWnd;
            GetWindowText();
        }

        /// <summary>
        /// Gets all child windows
        /// </summary>
        public void GetChildWindows()
        {
            User32.EnumChildWindows(Hwnd, (hwnd, lparam) =>
            {
                Window child = new Window(hwnd);
                child.GetChildWindows();
                Children.Add(child);
                return true;
            }, IntPtr.Zero);
        }

        /// <summary>
        /// Gets the window text
        /// </summary>
        private void GetWindowText()
        {
            const int MAX_CHAR = 256;
            StringBuilder sb = new StringBuilder(MAX_CHAR);
            User32.GetWindowText(Hwnd, sb, MAX_CHAR);
            Text = sb.ToString();
        }
    }
}
