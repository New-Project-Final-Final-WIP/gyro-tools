

public class Logger
{
    public LogType LoggingLevel = LogType.ERROR;

    public event LoggerEventHandler OnLog;
    public event LoggerEventHandler OnWarn;
    public event LoggerEventHandler OnError;

    public static DateTime CurTime => DateTime.Now;
    
    /// <summary>
    /// Logs an informational message
    /// </summary>
    /// <param name="obj">The object to log</param>
    /// <param name="runOnLog">Whether to run auxiliary logging functionality, true by default</param>
    public void LogInfo(object obj, bool runOnLog = true)
    {
        if (LoggingLevel < LogType.INFO)
            return;
        
        string msg = $"{CurTime:hh:mm:ss}  [INFO] {obj}";
        Console.WriteLine(msg);

        if (runOnLog)
        {
            OnLog?.Invoke(this, new(msg, LogType.INFO));
        }
    }

    /// <summary>
    /// Logs a warning message
    /// </summary>
    /// <param name="obj">The object to log</param>
    /// <param name="runOnLog">Whether to run auxiliary logging functionality, true by default</param>
    public void LogWarn(object obj, bool runOnLog = true)
    {
        if (LoggingLevel < LogType.WARN)
            return;
        
        string msg = $"{CurTime:hh:mm:ss}  [WARN] {obj}";
        Console.WriteLine(msg);

        if (runOnLog)
        {
            OnWarn?.Invoke(this, new(msg, LogType.WARN));
        }
    }

    /// <summary>
    /// Logs an error message
    /// </summary>
    /// <param name="obj">The object to log</param>
    /// <param name="runOnLog">Whether to run auxiliary logging functionality, true by default</param>
    public void LogError(object obj, bool runOnLog = true)
    {
        if (LoggingLevel < LogType.ERROR)
            return;
        
        string msg = $"{CurTime:hh:mm:ss} [ERROR] {obj}";
        Console.WriteLine(msg);

        if (runOnLog)
        {
            OnError?.Invoke(this, new(msg, LogType.ERROR));
        }
    }
}


public delegate void LoggerEventHandler(Logger source, LoggerEventArgs args);

public class LoggerEventArgs(string msg, LogType type) : EventArgs
{
    public string Msg = msg;
    public LogType Type = type;
}


/// <summary>
/// Denotes the type of log
/// </summary>
public enum LogType
{
    /// <summary>
    /// Denotes no log should be written
    /// </summary>
    NONE,

    /// <summary>
    /// Denotes an informational log
    /// </summary>
    INFO,

    /// <summary>
    /// Denotes a warning log
    /// </summary>
    WARN,

    /// <summary>
    /// Denotes an error log
    /// </summary>
    ERROR,
}