using System;
using UnityEngine;

namespace DSTA.VRNet.Core
{
    /// <summary>
    /// Persistent per-device player identity. Survives app restarts so the server
    /// can recognize a reconnecting headset as the same trainee.
    /// </summary>
    public static class PlayerIdentity
    {
        const string PrefsKey = "vrnet.playerGuid";

        public static string GetOrCreate()
        {
            var existing = PlayerPrefs.GetString(PrefsKey, "");
            if (Guid.TryParse(existing, out _)) return existing;

            var fresh = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(PrefsKey, fresh);
            PlayerPrefs.Save();
            return fresh;
        }

        public static void Reset() => PlayerPrefs.DeleteKey(PrefsKey);
    }
}
