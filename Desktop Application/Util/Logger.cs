using System;
using System.IO;

namespace BaldursGateInworld.Util
{
    public class Logger
    {
        private static Logger _instance;
        private static readonly object _lock = new object();
        private StreamWriter _logFile;

        private Logger()
        {
            _logFile = new StreamWriter("mod.log", true);
        }

        public static Logger Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new Logger();
                    }
                    return _instance;
                }
            }
        }

        public void Log(string message)
        {
            string logEntry = $"[{DateTime.Now}] {message}\n";
            _logFile.Write(logEntry);
            _logFile.Flush();
        }
    }

}
