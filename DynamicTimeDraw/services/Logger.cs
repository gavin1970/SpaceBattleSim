//using Chizl.IO.Logging;
//using System.Reflection;

////To Use It: Logger.Initialize();

//namespace DynamicTimeDraw
//{
//    internal static class Logger
//    {
//        static TextLogger _logger = TextLogger.Empty;
//        static LogLevel _logLevels = LogLevel.Application | LogLevel.Critical | LogLevel.Error | LogLevel.Information;
//        //static LogLevel _logLevels = LogLevel.All;
//        static LogLevel[] _debugAdd = { LogLevel.Debug, LogLevel.Trace };

//        static Logger()
//        {
//            var appName = Application.ProductName ?? Assembly.GetExecutingAssembly().GetName().Name;
//            _logger = new TextLogger(appName, ".\\logs");
//#if DEBUG
//            // If Debug mode, then add Debug and Trace to
//            // the LogLevel, but only if they don't exist.
//            // Since this only occurs on Static Initiation, 
//            // not worried about speed of HasFlag vs Bit Shift.
//            foreach (LogLevel level in _debugAdd)
//            {
//                if (!_logLevels.HasFlag(level))
//                    _logLevels = _logLevels | level;
//            }
//#endif
//            _logger.EnabledLogLevels = _logLevels;
//            _logger.KeepLogDays = TimeSpan.FromDays(7);
//            _logger.WriteLine(LogLevel.Application, $"{appName} Initialized");
//        }

//        public static void Initialize() { /* just to trigger the static constructor */ }
//        public static void LogApp(string message, bool addTime = true, bool eol = true) => LogMsg(LogLevel.Application, message, addTime, eol);
//        public static void LogInfo(string message, bool addTime = true, bool eol = true) => LogMsg(LogLevel.Information, message, addTime, eol);
//        public static void LogErr(string message, bool addTime = true, bool eol = true) => LogMsg(LogLevel.Error, message, addTime, eol);
//        public static void LogExc(string message, bool addTime = true, bool eol = true) => LogMsg(LogLevel.Critical, message, addTime, eol);
//        public static void LogDbg(string message, bool addTime = true, bool eol = true) => LogMsg(LogLevel.Debug, message, addTime, eol);
//        public static void LogTrace(string message, bool addTime = true, bool eol = true) => LogMsg(LogLevel.Trace, message, addTime, eol);
//        public static void LogAll(string message, bool addTime = true, bool eol = true) => LogMsg(LogLevel.All, message, addTime, eol);

//        private static void LogMsg(LogLevel level, string message, bool addTime = true, bool eol = true)
//        {
//            if (eol)
//                _logger.WriteLine(level, message, addTime);
//            else
//                _logger.Write(level, message, addTime);
//        }
//    }
//}
