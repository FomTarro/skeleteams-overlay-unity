using System;
using Microsoft.Extensions.Logging;
using UnityEngine;

public class YTLiveChatLogger<T> : ILogger<T>
{
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        string message = $"[{typeof(T).Name}] {formatter(state, exception)}";

        switch (logLevel)
        {
            case LogLevel.Error:
            case LogLevel.Critical:
                if (exception != null) Debug.LogException(exception);
                else Debug.LogError(message);
                break;
            case LogLevel.Warning:
                Debug.LogWarning(message);
                break;
            default:
                Debug.Log(message);
                break;
        }
    }
}
