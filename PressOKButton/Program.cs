using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PressOKButton
{
    /// <summary>
    /// Main Program
    /// </summary>
    class Program
    {
        #region Constants

        const int WM_KEYDOWN = 0x0100;
        const int WM_KEYUP = 0x0101;

        const int VK_RETURN = 0x0D;

        const string MUTEX_GUID = "CBB33422-7015-453C-A6F7-E8A61F2127EC",
                     NAMED_PIPE_GUID = "7D18DE81-E80B-4C46-A53D-04D616A76570";

        #endregion

        #region Fields
        static string m_processName = string.Empty,
                      m_messageText = string.Empty;

        static Thread m_namedPipeThread = null;
        static WindowWatchdog m_watchDog;

        #endregion


        /// <summary>
        /// Main entry point of the program.
        /// </summary>
        /// <param name="args">Input Arguments.</param>
        /// <returns></returns>
        static async Task Main(string[] args)
        {

            // Check the arguments
            if(args.Length == 0)
            {
                //display usage message
                DisplayArguments();
                return;
            }

            // create a mutex, shared between process, this ensures only one can watch an application at a time.
            using (Mutex mutex = new Mutex(false, MUTEX_GUID))
            {
                // read the arguments
                bool shutdownFlag = false;
                for (int i = 0; i < args.Length; i++)
                {
                    string upper = args[i].ToUpper();
                    if (upper == "--PROC")
                        m_processName = args[i + 1];
                    if (upper == "--MESSAGE")
                        m_messageText = args[i + 1];
                    if (upper == "--SHUTDOWN")
                    {
                        shutdownFlag = true;
                    }
                }

                // check the arguments
                if((m_processName == string.Empty || m_messageText == string.Empty)&&!shutdownFlag)
                {
                    Console.WriteLine($@"Arguments are incorrect:
    Process Name = ""{m_processName}""
         Message  = ""{m_messageText}""");
                    DisplayArguments();
                    return;
                }


                // check if another instance is open.
                bool instanceAlive = !mutex.WaitOne(TimeSpan.Zero);

                // display an error if another instance is open and we are not shutting down.
                if (instanceAlive && !shutdownFlag)
                {
                    Console.WriteLine("Only one instance can be open at a time use --shutdown to close the other instance.");
                    return;
                }

                // shut down the other instance
                else if (instanceAlive && shutdownFlag)
                {
                    ShutdownOtherInstance();
                    return;
                }

                //Create a named pipe, this is used to communicate across processes.
                CreateNamedPipe();

                // remove the file extension if it was passed in
                FileInfo fi = new FileInfo(m_processName);
                if (fi.Extension != null && fi.Extension != string.Empty)
                    m_processName = m_processName.Replace(fi.Extension, string.Empty);

                // start the watchdog
                m_watchDog = new WindowWatchdog(m_processName);
                m_watchDog.WindowOpened += Wd_WindowOpened;
                m_watchDog.WindowClosed += Wd_WindowClosed;
                m_watchDog.Start();

                // wait for the watchdog to shutdown.
                await Task.Run(() => m_watchDog.WaitForExit());
            }
        }

        /// <summary>
        /// Display a message with the correct usage of the arguments.
        /// </summary>
        private static void DisplayArguments()
        {
            Console.WriteLine(@"-- Press OK Button -- 
    Written by Benjamin Capuana - 4/15/21
  
    USAGE:
    --PROC
        - The process name to watch, next argument must be the process name
    --MESSAGE
        - The message to look for, the next argument must be the message, use quotes if your message has spaces
          ex. --MESSAGE ""oops an error has occured""
    --SHUTDOWN
        - shutsdown a currently running instance of the program.

  Press any key to continue...");

            Console.ReadKey();
        }

        /// <summary>
        /// Create a new named pipe for communication across processes.
        /// </summary>
        private static void CreateNamedPipe()
        {
            m_namedPipeThread = new Thread(() =>
            {
                NamedPipeServerStream pipeServer = new NamedPipeServerStream(NAMED_PIPE_GUID, PipeDirection.In, 1);
                pipeServer.WaitForConnection();

                Stream ioStream = pipeServer;
                int len = ioStream.ReadByte() * 256;
                len += ioStream.ReadByte();
                byte[] inBuffer = new byte[len];
                ioStream.Read(inBuffer, 0, len);

                UnicodeEncoding streamEncoding = new UnicodeEncoding();
                string message = streamEncoding.GetString(inBuffer);
                if (message == "SHUTDOWN")
                {
                    Console.WriteLine("Shutting Down...");
                    m_watchDog.Stop();
                }
            });
            m_namedPipeThread.IsBackground = true;
            m_namedPipeThread.Start();

        }

        /// <summary>
        /// Shutdown another instance
        /// </summary>
        private static void ShutdownOtherInstance()
        {
            const string LOOPBACK_IP = "127.0.0.1";
            NamedPipeClientStream pipeClientStream = new NamedPipeClientStream(LOOPBACK_IP, NAMED_PIPE_GUID, PipeDirection.Out);
            pipeClientStream.Connect();

            UnicodeEncoding streamEncoding = new UnicodeEncoding();

            byte[] outBuffer = streamEncoding.GetBytes("SHUTDOWN");
            int len = outBuffer.Length;
            if (len > UInt16.MaxValue)
            {
                len = (int)UInt16.MaxValue;
            }

            Stream ioStream = pipeClientStream;
            ioStream.WriteByte((byte)(len / 256));
            ioStream.WriteByte((byte)(len & 255));
            ioStream.Write(outBuffer, 0, len);
        }


        /// <summary>
        /// Event for when a window closes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Wd_WindowClosed(object sender, WindowClosedEventArgs e)
        {
            Console.WriteLine($"{e.Window.Hwnd} closed");
        }

        /// <summary>
        ///  Event for when a window opens
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Wd_WindowOpened(object sender, WindowOpenedEventArgs e)
        {
            Console.WriteLine($"{e.Window.Hwnd}, {e.Window.Text} opened");
            foreach (Window w in e.Window.Children)
            {
                Console.WriteLine($"\t{w.Hwnd},{w.Text}");
            }

            // press ok if the message was found.
            if (e.Window.Children.Find(cw => cw.Text == m_messageText && cw.Text != string.Empty) != null)
            {
                User32.PostMessage(e.Window.Children.Find(cw => cw.Text == "OK").Hwnd, WM_KEYDOWN, VK_RETURN, 1);
                User32.PostMessage(e.Window.Children.Find(cw => cw.Text == "OK").Hwnd, WM_KEYUP, VK_RETURN, 1);

            }
        }


    }
}
