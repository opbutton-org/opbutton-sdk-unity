using System;
using System.Collections.Generic;
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

        internal static OpButtonRunner Instance => s_instance;

        internal static void EnsureExists()
        {
            if (s_instance != null) return;

            var go = new GameObject("[OpButtonSDK]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            s_instance = go.AddComponent<OpButtonRunner>();
        }

        internal static void RunOnMainThread(Action action)
        {
            lock (s_pendingLock)
            {
                s_pendingActions.Add(action);
            }
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
