using System;
using System.IO;

namespace ResilientFileLock.Test
{
    internal class TestPath : IDisposable
    {
        private static readonly string TempFolderPath = Path.Combine(Path.GetTempPath(), "FileLockTests");

        public TestPath()
        {
            TempFile = new FileInfo(GetTempFileName());
        }

        public FileInfo TempFile { get; }

        public void Dispose()
        {
            TempFile.Delete();
        }

        private static string GetTempFileName()
        {
            if (!Directory.Exists(TempFolderPath))
            {
                Directory.CreateDirectory(TempFolderPath);
            }

            var filePath = Path.Combine(TempFolderPath, Path.GetRandomFileName());
            File.Create(filePath).Close();
            return filePath;
        }
    }
}