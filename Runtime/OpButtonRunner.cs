using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OpButton.SDK
{
    /// <summary>
    /// Internal MonoBehaviour that drives the OpButton SDK on the Unity main thread.
    /// Created automatically when OpButtonSDK.Initialize() is called.
    /// </summary>
    internal sealed class OpButtonRunner : MonoBehaviour
    {
        private static OpButtonRunner s_instance;
        private static readonly List<Action> s_pendingActions = new List<Action>();
        private static readonly object s_pendingLock = new object();
        private static int s_mainThreadId;

        internal static OpButtonRunner Instance => s_instance;

        internal static void EnsureExists()
        {
            if (s_instance != null) return;

            s_mainThreadId = Thread.CurrentThread.ManagedThreadId;

            var go = new GameObject("[OpButtonSDK]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            s_instance = go.AddComponent<OpButtonRunner>();
        }

        internal static void RunOnMainThread(Action action)
        {
            if (Thread.CurrentThread.ManagedThreadId == s_mainThreadId)
            {
                action();
                return;
            }
            lock (s_pendingLock)
            {
                s_pendingActions.Add(action);
            }
        }

        internal static T InvokeOnMainThread<T>(Func<T> operation, int timeoutMs = 15000,
            CancellationToken cancellation = default(CancellationToken))
        {
            cancellation.ThrowIfCancellationRequested();
            if (Thread.CurrentThread.ManagedThreadId == s_mainThreadId) return operation();
            var completion = new TaskCompletionSource<T>();
            RunOnMainThread(() =>
            {
                if (completion.Task.IsCompleted || cancellation.IsCancellationRequested) return;
                try { completion.TrySetResult(operation()); }
                catch (Exception error) { completion.TrySetException(error); }
            });
            bool completed;
            try { completed = completion.Task.Wait(timeoutMs, cancellation); }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled();
                throw;
            }
            catch (AggregateException error)
            {
                throw new OpButtonException("Unity main thread request failed", error.InnerException ?? error);
            }
            if (!completed)
            {
                completion.TrySetCanceled();
                throw new OpButtonException("Unity main thread did not process the SDK request");
            }
            return completion.Task.GetAwaiter().GetResult();
        }

        private void Update()
        {
            FlushPendingActions();
            OpButtonSDK.OnUpdate();
        }

        private void FlushPendingActions()
        {
            List<Action> actions;
            lock (s_pendingLock)
            {
                if (s_pendingActions.Count == 0) return;
                actions = new List<Action>(s_pendingActions);
                s_pendingActions.Clear();
            }

            foreach (var action in actions)
            {
                try { action(); }
                catch (Exception e) { OpButtonLog.Error("OpButtonRunner main-thread action failed", e); }
            }
        }

        private void OnApplicationQuit()
        {
            OpButtonSDK.OnApplicationQuit();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }
    }
}
