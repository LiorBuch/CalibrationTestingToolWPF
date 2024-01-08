using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.Text;
using NLog;
using System.IO;

namespace CalibrationToolTester.GlobalLoger
{
    public static class Logger
    {
        public static EventLog eventLog = null;
        public static Stopwatch globalStopWatch = new Stopwatch();
        private static string logFileName;
        private static object locker;

        private static UdpClient udpClient = new UdpClient();
        private static IPAddress serverAddress = null;
        private static IPEndPoint endPoint = null;
        public static string udpEndPointIPAddress = "192.168.201.142";
        public static int udpEndPointPort = 27000;

        private static NLog.Logger logger = LogManager.GetCurrentClassLogger();

        static Logger()
        {
            try
            {
                locker = new object();
                //Object _logFileName = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ScanMaster\\1000Gates", "LogFile", null);
                string _logFileName = AppDomain.CurrentDomain.BaseDirectory + "LogFile.txt";

                if (string.IsNullOrEmpty(_logFileName) == false)
                {
                    logFileName = _logFileName;
                    File.Delete(logFileName);
                }
                else
                {
                    logFileName = null;
                }

                udpClient.Client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                //var config = new NLog.Config.LoggingConfiguration();

                //var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "file.txt" };
                //var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

                //// Rules for mapping loggers to targets
                //config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
                //config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

                // Apply config
                //NLog.LogManager.Configuration = config;
            }
            catch (Exception)
            {
            }
        }

        public static void WriteMessage(string message, EventLogEntryType messageType = EventLogEntryType.Information)
        {
            try
            {
#if DEBUG

                Debug.WriteLine("[" + DateTime.Now.ToLongTimeString() + "]" + "[" + messageType.ToString() + "]" + message);
                logger?.Debug("[" + messageType.ToString() + "]" + message);

#endif

                eventLog?.WriteEntry(message, messageType);
                WriteToLogFile(message, messageType);
            }
            catch (Exception)
            {
            }
        }

        public static void WriteMessageToExternal(string message)
        {
            byte[] send_buffer = null;

            try
            {
                serverAddress = IPAddress.Parse(udpEndPointIPAddress);
                endPoint = new IPEndPoint(serverAddress, udpEndPointPort);

                send_buffer = Encoding.ASCII.GetBytes(message);

                udpClient.Client.SendTo(send_buffer, endPoint);
            }
            catch (Exception)
            {
            }
        }

        public static void ExceptionHandler(Exception exception, string message)
        {
            try
            {
#if DEBUG

                Debug.WriteLine("[" + DateTime.Now.ToLongTimeString() + "]" + "[" + EventLogEntryType.Error.ToString() + "]" + message);
                logger?.Error(exception, message);
#endif

                eventLog?.WriteEntry(exception.Message, EventLogEntryType.Error);
                WriteToLogFile(exception.Message, EventLogEntryType.Error);

                logger?.Error(exception);
            }
            catch (Exception)
            {
            }
        }

        public static void WriteToLogFile(string message, EventLogEntryType messageType)
        {
            try
            {
                lock (locker)
                {
                    if (logFileName != null)
                    {
                        if (logFileName != string.Empty)
                        {
                            File.AppendAllText(logFileName, "[" + messageType + "]" + "[" + DateTime.Now + "]" + message + "\n");
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}