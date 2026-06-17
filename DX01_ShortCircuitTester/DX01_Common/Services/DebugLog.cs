using System;

namespace DX01_ShortCircuitTester.Services
{
    /// <summary>Log 分類，UI 依此著色。</summary>
    public enum LogKind
    {
        Info,
        Relay,
        Tx,
        Rx,
        Error
    }

    public sealed class LogEventArgs : EventArgs
    {
        public DateTime Time { get; }
        public LogKind Kind { get; }
        public string Message { get; }

        public LogEventArgs(DateTime time, LogKind kind, string message)
        {
            Time = time;
            Kind = kind;
            Message = message;
        }
    }

    /// <summary>
    /// 簡易除錯日誌匯流排：設備控制器寫入，UI 訂閱 <see cref="Entry"/> 顯示。
    /// 用於實機驗證時觀察 Relay 切換、SCPI 指令、回傳值與錯誤。
    /// </summary>
    public sealed class DebugLog
    {
        public event EventHandler<LogEventArgs> Entry;

        public void Write(LogKind kind, string message)
        {
            var handler = Entry;
            if (handler != null)
                handler(this, new LogEventArgs(DateTime.Now, kind, message));
        }
    }
}
