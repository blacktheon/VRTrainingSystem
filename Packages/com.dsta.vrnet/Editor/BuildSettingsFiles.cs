using System;
using System.IO;

namespace DSTA.VRNet.Editor
{
    /// <summary>Restores build settings even while another reader keeps the old file mapped.</summary>
    public static class BuildSettingsFiles
    {
        public static void Restore(string path, byte[] originalBytes)
        {
            if (originalBytes == null) throw new ArgumentNullException(nameof(originalBytes));
            var destination = Path.GetFullPath(path);
            var temporary = destination + ".vrnet-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    file.Write(originalBytes, 0, originalBytes.Length);
                // Truncating an existing memory-mapped file fails on Windows (error 1224).
                // Replace its directory entry instead, preserving readers of the old file.
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
