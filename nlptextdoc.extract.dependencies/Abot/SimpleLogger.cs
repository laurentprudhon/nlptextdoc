using System;
using System.IO;

/// <summary>
/// In-place replacement for the heavy log4net package in this simple context
/// </summary>
namespace log4net
{
    public enum LogLevel
    {
        Debug = 5,
        Info  = 4,
        Warn  = 3,
        Error = 2,
        Fatal = 1
    }

    public interface ILog
    {
        bool IsDebugEnabled { get; }

        void Debug(object messageSource);

        void Info(object messageSource);

        void Warn(object messageSource);

        void Error(object messageSource);

        void Fatal(object messageSource);

        void DebugFormat(string format, params object[] args);

        void InfoFormat(string format, params object[] args);

        void WarnFormat(string format, params object[] args);

        void ErrorFormat(string format, params object[] args);

        void FatalFormat(string format, params object[] args);
    }

    public class LogManager
    {
        private static ILog singletonLogger;
        internal static TextWriter outputWriter = Console.Out;

        public static void SetTextWriter(TextWriter textWriter)
        {
            outputWriter = textWriter;
        }

        public static ILog GetLogger(string name)
        {
            if (singletonLogger == null)
            {
                singletonLogger = new SimpleLogger();
            }
            return singletonLogger;
        }
    }

    public class SimpleLogger : ILog
    {
        public LogLevel LogLevel = LogLevel.Error;

        private static void WriteLog(string level, string message)
        {
            try
            {
                lock (LogManager.outputWriter)
                {
                    LogManager.outputWriter.WriteLine(level + ": " + message);
                    LogManager.outputWriter.Flush();
                }
            }
            catch
            {
                Console.Out.WriteLine(level + ": " + message);
                Console.Out.Flush();
            }
        }

        public bool IsDebugEnabled
        {
            get { return LogLevel == LogLevel.Debug; }
        }

        public void Debug(object messageSource)
        {
            if (LogLevel >= LogLevel.Debug)
            {
                string message = messageSource.ToString();
                WriteLog("debug", message);
            }
        }

        public void Info(object messageSource)
        {
            if (LogLevel >= LogLevel.Info)
            {
                string message = messageSource.ToString();
                WriteLog("info", message);
            }
        }

        public void Warn(object messageSource)
        {
            if (LogLevel >= LogLevel.Warn)
            {
                string message = messageSource.ToString();
                WriteLog("warning", message);
            }
        }

        public void Error(object messageSource)
        {
            if (LogLevel >= LogLevel.Error)
            {
                string message = messageSource.ToString();
                WriteLog("error", message);
            }
        }

        public void Fatal(object messageSource)
        {
            if (LogLevel >= LogLevel.Fatal)
            {
                string message = messageSource.ToString();
                WriteLog("fatal", message);
            }
        }

        public void DebugFormat(string format, params object[] args)
        {
            if (LogLevel >= LogLevel.Debug)
            {
                string message = String.Format(format, args);
                WriteLog("debug", message);
            }
        }

        public void InfoFormat(string format, params object[] args)
        {
            if (LogLevel >= LogLevel.Info)
            {
                string message = String.Format(format, args);
                WriteLog("info", message);
            }
        }

        public void WarnFormat(string format, params object[] args)
        {
            if (LogLevel >= LogLevel.Warn)
            {
                string message = String.Format(format, args);
                WriteLog("warning", message);
            }
        }

        public void ErrorFormat(string format, params object[] args)
        {
            if (LogLevel >= LogLevel.Error)
            {
                string message = String.Format(format, args);
                WriteLog("error", message);
            }
        }

        public void FatalFormat(string format, params object[] args)
        {
            if (LogLevel >= LogLevel.Fatal)
            {
                string message = String.Format(format, args);
                WriteLog("fatal", message);
            }
        }        
    }
}
