using System;
using System.Collections.Generic;
using System.Threading;

namespace OpButton.SDK
{
    /// <summary>
    /// Internal SDK logger. Delegates to IOpButtonLoggerListener if set, otherwise to UnityEngine.Debug.
    /// When called from a background thread, log messages are queued and flushed on the main thread.
    /// </summary>
    internal static class OpButtonLog
    {
        private const string TAG = "OpButtonSDK";
        private static OpButtonLogLevel s_level = OpButtonLogLevel.Info;
        private static volatile IOpButtonLoggerListener s_listener;
        private static int s_mainThreadId;

        private static readonly List<DeferredLog> s_deferred = new List<DeferredLog>();
        private static readonly object s_deferredLock = new object();

        private struct DeferredLog
        {
            public OpButtonLogLevel Level;
            public string Message;
            public Exception Exception;
        }

        internal static void SetMainThreadId(int threadId) { s_mainThreadId = threadId; }
        internal static void SetLevel(OpButtonLogLevel level) { s_level = level; }
        internal static void SetLoggerListener(IOpButtonLoggerListener listener) { s_listener = listener; }

        internal static void Debug(string message, params object[] args)
        {
            if (s_level > OpButtonLogLevel.Debug) return;
            string msg = Format(message, args);
            var listener = s_listener;
            if (listener != null)
            {
                listener.OnOpButtonLoggerMessage(OpButtonLogLevel.Debug, TAG, msg, null);
                return;
            }
            if (!IsMainThread())
            {
                Defer(OpButtonLogLevel.Debug, msg, null);
                return;
            }
            UnityEngine.Debug.Log($"[{TAG}] {msg}");
        }

        internal static void Info(string message, params object[] args)
        {
            if (s_level > OpButtonLogLevel.Info) return;
            string msg = Format(message, args);
            var listener = s_listener;
            if (listener != null)
            {
                listener.OnOpButtonLoggerMessage(OpButtonLogLevel.Info, TAG, msg, null);
                return;
            }
            if (!IsMainThread())
            {
                Defer(OpButtonLogLevel.Info, msg, null);
                return;
            }
            UnityEngine.Debug.Log($"[{TAG}] {msg}");
        }

        internal static void Warn(string message, params object[] args)
        {
            if (s_level > OpButtonLogLevel.Warning) return;
            string msg = Format(message, args);
            var listener = s_listener;
            if (listener != null)
            {
                listener.OnOpButtonLoggerMessage(OpButtonLogLevel.Warning, TAG, msg, null);
                return;
            }
            if (!IsMainThread())
            {
                Defer(OpButtonLogLevel.Warning, msg, null);
                return;
            }
            UnityEngine.Debug.LogWarning($"[{TAG}] {msg}");
        }

        internal static void Error(string message, params object[] args)
        {
            if (s_level > OpButtonLogLevel.Error) return;
            string msg = Format(message, args);
            var listener = s_listener;
            if (listener != null)
            {
                listener.OnOpButtonLoggerMessage(OpButtonLogLevel.Error, TAG, msg, null);
                return;
            }
            if (!IsMainThread())
            {
                Defer(OpButtonLogLevel.Error, msg, null);
                return;
            }
            UnityEngine.Debug.LogError($"[{TAG}] {msg}");
        }

        internal static void Error(string message, Exception e)
        {
            var listener = s_listener;
            if (listener != null)
            {
                listener.OnOpButtonLoggerMessage(OpButtonLogLevel.Exception, TAG, message, e);
                return;
            }
            if (!IsMainThread())
            {
                Defer(OpButtonLogLevel.Exception, message, e);
                return;
            }
            UnityEngine.Debug.LogError($"[{TAG}] {message}: {e}");
        }

        private static bool IsMainThread()
        {
            return s_mainThreadId == 0 || Thread.CurrentThread.ManagedThreadId == s_mainThreadId;
        }

        private static void Defer(OpButtonLogLevel level, string message, Exception e)
        {
            lock (s_deferredLock)
            {
                s_deferred.Add(new DeferredLog { Level = level, Message = message, Exception = e });
            }
        }

        internal static void FlushDeferred()
        {
            List<DeferredLog> logs;
            lock (s_deferredLock)
            {
                if (s_deferred.Count == 0) return;
                logs = new List<DeferredLog>(s_deferred);
                s_deferred.Clear();
            }

            foreach (var log in logs)
            {
                switch (log.Level)
                {
                    case OpButtonLogLevel.Debug:
                    case OpButtonLogLevel.Info:
                        UnityEngine.Debug.Log($"[{TAG}] {log.Message}");
                        break;
                    case OpButtonLogLevel.Warning:
                        UnityEngine.Debug.LogWarning($"[{TAG}] {log.Message}");
                        break;
                    case OpButtonLogLevel.Error:
                        UnityEngine.Debug.LogError($"[{TAG}] {log.Message}");
                        break;
                    case OpButtonLogLevel.Exception:
                        UnityEngine.Debug.LogError($"[{TAG}] {log.Message}: {log.Exception}");
                        break;
                }
            }
        }

        private static string Format(string message, object[] args)
        {
            if (args == null || args.Length == 0) return message;
            return string.Format(message, args);
        }
    }
}
