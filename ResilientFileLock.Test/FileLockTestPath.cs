using System.IO;

namespace ResilientFileLock.Test
{
    internal class FileLockTestPath
    {
        public static readonly string TempFolderPath = Path.Combine(Path.GetTempPath(), "FileLockTests");

        public static string GetTempFileName()
        {
            if (!Directory.Exists(TempFolderPath))
            {
                Directory.CreateDirectory(TempFolderPath);
            }
            var filePath = Path.Combine(TempFolderPath, Path.GetRandomFileName());
            File.Create(filePath).Close();
            return filePath;
        }

        public static string ChangeExtension(string path, string extension)
        {
            return Path.ChangeExtension(path, extension);
        }
    }
}