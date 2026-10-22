using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using NUnit.Framework;
using UnityEngine;

namespace DSTA.VRNet.Tests
{
    public class BuildSettingsFilesTests
    {
        string _directory;
        string _path;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "vrnet-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, "settings.asset");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_directory, true);

        [TestCase("longer temporary build settings")]
        [TestCase("x")]
        public void Restore_ReplacesChangedBytesAndLeavesNoTemporaryFiles(string changed)
        {
            var original = new byte[] { 1, 2, 3, 4 };
            File.WriteAllText(_path, changed);
            Restore(original);
            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(_directory), Is.EqualTo(new[] { _path }));
        }

        [Test]
        public void Restore_RecreatesADeletedSettingsFile()
        {
            var original = new byte[] { 9, 8, 7 };
            Restore(original);
            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(original));
        }

        [Test]
        public void Restore_WorksWhileWindowsKeepsTheOldFileMemoryMapped()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("This regression reproduces Windows' mapped-file truncation error.");
            var original = new byte[] { 1, 2, 3 };
            var changed = new byte[] { 4, 5, 6, 7, 8 };
            File.WriteAllBytes(_path, changed);
            using (var file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var mapping = MemoryMappedFile.CreateFromFile(file, null, 0, MemoryMappedFileAccess.Read,
                HandleInheritability.None, false))
            using (var view = mapping.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
            {
                Assert.Throws<IOException>(() => File.WriteAllBytes(_path, original),
                    "The old truncate-in-place approach must reproduce the Windows failure.");
                Assert.DoesNotThrow(() => Restore(original));
                Assert.That(File.ReadAllBytes(_path), Is.EqualTo(original));
                Assert.That(view.ReadByte(0), Is.EqualTo(changed[0]), "Existing readers retain their original mapping.");
            }
            Assert.That(Directory.GetFiles(_directory), Is.EqualTo(new[] { _path }));
        }

        void Restore(byte[] original)
        {
            var type = Type.GetType("DSTA.VRNet.Editor.BuildSettingsFiles, DSTA.VRNet.Editor");
            Assert.That(type, Is.Not.Null, "Shared build-settings file restoration must be available.");
            type.GetMethod("Restore").Invoke(null, new object[] { _path, original });
        }
    }
}
