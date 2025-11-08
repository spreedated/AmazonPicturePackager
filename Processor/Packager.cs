using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Processor
{
    public class Packager
    {
        private readonly ILogger logger;
        private string tempPath;
        private string imagefile;
        private string copypath;
        private string outputPath;
        private string[] asins;
        public string BasePath { get; set; }

        public event EventHandler<int> CurrentPackedFilesCountChanged;

        #region Ctor
        public Packager(string basePath, ILogger logger = null)
        {
            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
            {
                throw new FileNotFoundException("Invalid BasePath", nameof(basePath));
            }

            this.logger = logger;
            this.BasePath = basePath;
        }
        #endregion

        private static bool IsDirectoryValidCreate(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            return Directory.Exists(directoryPath);
        }

        private async Task CreateImageFiles(string originalFilepath, string amazonPictureCode, CancellationToken token)
        {
            if (this.asins == null || this.asins.Length == 0)
            {
                return;
            }

            if (string.IsNullOrEmpty(this.imagefile) || !File.Exists(this.imagefile))
            {
                throw new FileNotFoundException("Source image not found", this.imagefile);
            }

            List<Task> copyTasks = [];

            foreach (string a in this.asins)
            {
                copyTasks.Add(Task.Run(() =>
                {
                    File.Copy(this.imagefile, Path.Combine(this.copypath, $"{a}.{amazonPictureCode}.{Path.GetExtension(originalFilepath).Replace(".", "")}"), true);
                    this.logger?.LogTrace("Created image for ASIN: {Asin}", a);
                }, token));
            }

            await Task.WhenAll(copyTasks);
        }

        private async Task CreateZipFile(int filesPerZip, CancellationToken token)
        {
            await Task.Run(() =>
            {
                int currentFilesPacked = 0;
                int zipNumber = 1;
                int zipNumberFilename = 1;

                for (int i = 0; i < (int)Math.Ceiling(Directory.GetFiles(this.copypath).Length / (float)filesPerZip); i++)
                {
                    string zipPath = Path.Combine(this.outputPath, $"pack{zipNumberFilename:000}.zip");

                    while (File.Exists(zipPath) && !token.IsCancellationRequested)
                    {
                        zipPath = Path.Combine(this.outputPath, $"pack{zipNumberFilename:000}.zip");
                        zipNumberFilename++;
                    }

                    using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                    {
                        foreach (string asinImage in Directory.GetFiles(this.copypath).Skip((zipNumber - 1) * filesPerZip).Take(filesPerZip))
                        {
                            if (token.IsCancellationRequested)
                            {
                                break;
                            }

                            zip.CreateEntryFromFile(asinImage, Path.GetFileName(asinImage));
                            currentFilesPacked++;

                            //Globals.AppStatus.Change($"Processing [{currentFilesPacked}/{this.asins.Count}]", true);
                            this.logger?.LogInformation("Processing [{Fpacked}/{Count}]", currentFilesPacked, asins.Length);
                            this.CurrentPackedFilesCountChanged?.Invoke(this, currentFilesPacked);
                        }
                        zipNumber++;
                        zipNumberFilename++;
                    }
                }
            }, token);
        }

        public async Task<bool> PackAsync(string originalFilepath, IEnumerable<string> asinList, string amazonPictureCode, int filesPerZip, bool autoOpenOutputDirectory, CancellationToken token)
        {
            if (string.IsNullOrEmpty(originalFilepath) || !File.Exists(originalFilepath))
            {
                throw new FileNotFoundException("Invalid Picturepath", nameof(originalFilepath));
            }

            this.tempPath = Path.Combine(this.BasePath, "temp");

            if (!IsDirectoryValidCreate(this.tempPath))
            {
                this.logger?.LogError("Cannot create directory");
                return false;
            }

            this.imagefile = Path.Combine(this.tempPath, Path.GetFileName(originalFilepath));

            //Globals.AppStatus.Change("Copying...", true);
            this.logger?.LogInformation("Copying file to temp");

            await Task.Run(() => File.Copy(originalFilepath, imagefile, true), token);

            if (!File.Exists(Path.Combine(this.tempPath, imagefile)))
            {
                this.logger?.LogError("Error copying file to temp");
                return false;
            }

            //Globals.AppStatus.Change("File copied to temp", true);
            this.logger?.LogInformation("File copied to temp");

            this.copypath = Path.Combine(this.tempPath, "files");

            if (!IsDirectoryValidCreate(this.copypath))
            {
                this.logger?.LogError("Cannot create directory");
                return false;
            }

            this.asins = [.. asinList.Select(x => x.Replace("\r", "").Replace("\n", "").Replace("\r\n", "").ToUpper()).Where(x => !string.IsNullOrEmpty(x))];

            await this.CreateImageFiles(originalFilepath, amazonPictureCode, token);

            this.outputPath = Path.Combine(this.BasePath, "output");

            if (!IsDirectoryValidCreate(outputPath))
            {
                this.logger?.LogError("Cannot create directory");
                return false;
            }

            //Globals.AppStatus.Change("Processing...", true);
            this.logger?.LogInformation("Starting packaging process");

            await this.CreateZipFile(filesPerZip, token);

            //Globals.AppStatus.Change("Cleaning...", true);
            this.logger?.LogInformation("Cleaning temp files");

            await Task.Run(() => Directory.Delete(this.tempPath, true), token);

            if (token.IsCancellationRequested)
            {
                return false;
            }

            if (autoOpenOutputDirectory)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }

            return true;
        }
    }
}
