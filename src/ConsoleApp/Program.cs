using System;
using System.IO;
using System.Runtime.Caching;
using System.Threading;
using PgpCore;

namespace Encryptor.ConsoleApp
{
    class Program
    {
        private static string _keyPath = Path.Combine(Directory.GetCurrentDirectory(), "PublicKey");
        private static string _inputDir = Path.Combine(Directory.GetCurrentDirectory(), "Input");
        private static string _outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Output");
        static void Main(string[] args)
        {
            if (Directory.Exists(_inputDir) == false)
            {
                Console.WriteLine($"Creating input directory: \"{_inputDir}\".");
                Directory.CreateDirectory(_inputDir);
            }

            if (Directory.Exists(_outputDir) == false)
            {
                Console.WriteLine($"Creating output directory: \"{_outputDir}\".");
                Directory.CreateDirectory(_outputDir);
            }

            while (File.Exists(_keyPath) == false)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"Please, put the public key in \"{_keyPath}\".");
                Console.ForegroundColor = ConsoleColor.White;
                Wait("Press a key to continue...");
            }

            var cache = MemoryCache.Default;
            
            using (var fileWatcher = new FileSystemWatcher())
            {
                Console.WriteLine($"Listening to file changes in \"{_inputDir}\".");
                fileWatcher.Path = _inputDir;
                fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                fileWatcher.Filter = "*.*";
                fileWatcher.Changed += (source, @event) =>
                {
                    Console.WriteLine($"Detected file \"{@event.FullPath}\"");
                    cache.AddOrGetExisting(@event.FullPath, @event,
                        new CacheItemPolicy
                        {
                            AbsoluteExpiration = DateTime.UtcNow.AddSeconds(10),
                            RemovedCallback = EncryptFile
                        });
                };
                fileWatcher.IncludeSubdirectories = true;
                fileWatcher.EnableRaisingEvents = true;
                Wait("Press a key to exit...");
            } 
        }

        static void EncryptFile(CacheEntryRemovedArguments arguments)
        {
            if (arguments.RemovedReason != CacheEntryRemovedReason.Expired)
            {
                return;
            }

            var @event = (FileSystemEventArgs) arguments.CacheItem.Value;
            Console.WriteLine($"Encrypting file \"{@event.FullPath}\".");
            try
            {
                using (var pgp = new PGP())
                {
                    var output = @event.FullPath.Replace(_inputDir, _outputDir);

                    pgp.EncryptFile(
                        @event.FullPath,
                        output,
                        _keyPath);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"File encrypted to \"{output}\" with \"{_keyPath}\".\n");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to encrypt file \"{@event.FullPath}\".\nError message: \"{ex.Message}\"");
            }
            finally
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Press a key to exit...");
            }
        }

        static void Wait(string message)
        {
            Console.WriteLine(message);
            Console.ReadKey();
        }
    }
}
