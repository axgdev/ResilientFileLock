using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ResilientFileLock
{
    internal class LockModel
    {
        private readonly string _path;
        private readonly Guid _identifier;

        private class LockFile
        {
            public Guid Identifier = Guid.Empty;
            public DateTime ReleaseDate = DateTime.MinValue;
        }

        internal LockModel(string path)
        {
            _path = path;
            _identifier = Guid.NewGuid();
        }

        internal async Task<bool> TrySetReleaseDate(DateTime date)
        {
            try
            {
                using (var fs = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                using (var sr = new StreamWriter(fs, Encoding.UTF8))
                {
                    await sr.WriteLineAsync(_identifier.ToString());
                    await sr.WriteLineAsync(date.Ticks.ToString());
                }
            }
            catch(Exception)
            {
                return false;
            }
            return true;
        }

        internal async Task<bool> IsInstanceOwned(string path = "")
        {
            return _identifier == await GetIdentifier(path);
        }

        internal async Task<DateTime> GetReleaseDate(string path = "")
        {
            var lockFile = await TryGetLockFile(path);
            return lockFile.ReleaseDate;
        }

        private async Task<Guid> GetIdentifier(string path = "")
        {
            var lockFile = await TryGetLockFile(path);
            return lockFile.Identifier;
        }

        private async Task<LockFile> TryGetLockFile(string path = "")
        {
            try
            {
                using (var fs = new FileStream(string.IsNullOrWhiteSpace(path) ? _path : path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    var identifier = Guid.Parse(await sr.ReadLineAsync());
                    var ticks = long.Parse(await sr.ReadLineAsync());
                    var releaseDate = new DateTime(ticks, DateTimeKind.Utc);

                    return new LockFile { Identifier = identifier, ReleaseDate = releaseDate };
                }
            }
            catch(Exception)
            {
                return new LockFile();
            }
        }
    }
}
