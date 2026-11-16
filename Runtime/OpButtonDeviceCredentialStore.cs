using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace OpButton.SDK
{
    internal sealed class OpButtonDeviceCredentialStore
    {
        private readonly object m_lock = new object();
        private readonly System.Collections.Generic.Dictionary<string, string> m_memory =
            new System.Collections.Generic.Dictionary<string, string>();

        internal string GetOrCreate(string baseUrl, string deviceId, string configured)
        {
            if (!string.IsNullOrEmpty(configured)) return configured;
            string key = Key(baseUrl, deviceId);
            lock (m_lock)
            {
                if (m_memory.TryGetValue(key, out var cached) && Valid(cached)) return cached;
                string path = CredentialPath(key);
                if (File.Exists(path))
                {
                    if (new FileInfo(path).Length > 256)
                        throw new OpButtonException("Stored device credential is invalid");
                    string current = File.ReadAllText(path, Encoding.UTF8).Trim();
                    if (Valid(current))
                    {
                        m_memory[key] = current;
                        return current;
                    }
                    throw new OpButtonException("Stored device credential is invalid");
                }

                byte[] bytes = new byte[48];
                using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
                string value = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                SaveByKey(key, value);
                return value;
            }
        }

        internal void Save(string baseUrl, string deviceId, string value)
        {
            if (!Valid(value))
                throw new ArgumentException("Device credential must contain 32 to 256 characters");
            string key = Key(baseUrl, deviceId);
            lock (m_lock) SaveByKey(key, value);
        }

        private void SaveByKey(string key, string value)
        {
            string path = CredentialPath(key);
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, value, new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);
#if UNITY_IOS && !UNITY_EDITOR
                UnityEngine.iOS.Device.SetNoBackupFlag(path);
#endif
                m_memory[key] = value;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static bool Valid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 32 || value.Length > 256)
                return false;
            foreach (char character in value)
                if (character < 0x21 || character > 0x7e) return false;
            return true;
        }

        private static string Key(string baseUrl, string deviceId)
        {
            using (var sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(baseUrl + "\0" + deviceId));
                var output = new StringBuilder("opbutton.device.", 80);
                foreach (byte value in digest) output.Append(value.ToString("x2"));
                return output.ToString();
            }
        }

        private static string CredentialPath(string key)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            using (var directory = context.Call<AndroidJavaObject>("getNoBackupFilesDir"))
                return Path.Combine(directory.Call<string>("getAbsolutePath"), key + ".credential");
#else
            return Path.Combine(Application.persistentDataPath, ".opbutton", key + ".credential");
#endif
        }
    }
}
