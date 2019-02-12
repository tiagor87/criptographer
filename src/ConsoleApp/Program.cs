using System;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Threading;
using PgpCore;

namespace Encryptor.ConsoleApp
{
    internal static class Program
    {
        private static string _keyPath;
        private static readonly string InputDir = Path.Combine(Directory.GetCurrentDirectory(), "Input");
        private static readonly string OutputDir = Path.Combine(Directory.GetCurrentDirectory(), "Output");

        private static void Main(string[] args)
        {
            if (Directory.Exists(InputDir) == false)
            {
                WriteLineInColor($"Creating input directory: \"{InputDir}\".", ConsoleColor.Blue);
                Directory.CreateDirectory(InputDir);
            }

            if (Directory.Exists(OutputDir) == false)
            {
                WriteLineInColor($"Creating output directory: \"{OutputDir}\".", ConsoleColor.Blue);
                Directory.CreateDirectory(OutputDir);
            }

            
            while (TryGetKeyFilePath(out _keyPath) == false)
            {
                WriteLineInColor($"Please, put the public key in \"{Directory.GetCurrentDirectory()}\" with extension \"pgp\", \"gpg\" or \"asc\".", ConsoleColor.DarkYellow);
                Wait();
            }

            WriteLineInColor($"Found public key in \"{_keyPath}\".", ConsoleColor.Green);

            var cache = MemoryCache.Default;
            
            using (var fileWatcher = new FileSystemWatcher())
            {
                WriteLineInColor($"Please, put the files to encrypting in \"{InputDir}\".", ConsoleColor.DarkYellow);
                fileWatcher.Path = InputDir;
                fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
                fileWatcher.Filter = "*.*";
                fileWatcher.Changed += (source, @event) =>
                {
                    WriteLineInColor($"Encrypting file \"{@event.Name}\"...", ConsoleColor.Blue);

                    if (cache.Add(@event.FullPath, @event,
                        new CacheItemPolicy
                        {
                            SlidingExpiration = TimeSpan.FromMilliseconds(250),
                            RemovedCallback = EncryptFile
                        }))
                    {
                        return;
                    }
                    
                    // If exists
                    // Remove and add it again to update expiration
                    cache.Remove(@event.FullPath);
                    cache.Add(@event.FullPath, @event,
                        new CacheItemPolicy
                        {
                            SlidingExpiration = TimeSpan.FromMilliseconds(250),
                            RemovedCallback = EncryptFile
                        });
                };
                fileWatcher.IncludeSubdirectories = true;
                fileWatcher.EnableRaisingEvents = true;
                Wait();
            } 
        }

        private static void EncryptFile(CacheEntryRemovedArguments arguments)
        {
            if (arguments.RemovedReason != CacheEntryRemovedReason.Expired)
            {
                return;
            }

            var @event = (FileSystemEventArgs) arguments.CacheItem.Value;
            try
            {
                using (var pgp = new PGP())
                {
                    var output = @event.FullPath.Replace(InputDir, OutputDir);
                    
                    pgp.EncryptFile(
                        @event.FullPath,
                        output,
                        _keyPath);

                    WriteLineInColor($"File was successful encrypted.", ConsoleColor.Green);
                    WriteLineInColor($"Input: {@event.Name};\nOutput: \"{Path.GetFileName(output)}\";\nkey: \"{Path.GetFileName(_keyPath)}\".", ConsoleColor.DarkGreen);
                }
            }
            catch (Exception ex)
            {
                WriteLineInColor($"Failed to encrypt file \"{@event.Name}\".\nError message: \"{ex.Message}\"", ConsoleColor.Red);
            }
        }

        private static void Wait()
        {
            Console.ReadKey();
        }

        private static bool TryGetKeyFilePath(out string keyPath)
        {
            keyPath = Directory.GetFiles(Directory.GetCurrentDirectory())
                .FirstOrDefault(x =>
                    x.EndsWith("gpg", StringComparison.InvariantCultureIgnoreCase)
                    || x.EndsWith("pgp", StringComparison.InvariantCultureIgnoreCase)
                    || x.EndsWith("asc", StringComparison.InvariantCultureIgnoreCase));
            return string.IsNullOrWhiteSpace(keyPath) == false;
        }

        private static string _oldMessage;

        private static void WriteLineInColor(string message, ConsoleColor color)
        {
            if (_oldMessage == message)
            {
                return;
            }

            _oldMessage = message;
            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = prevColor;
        }
    }
}
