using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AmazonPicturePackager.Logic;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AmazonPicturePackager.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly List<string> asins = [];
        private CancellationTokenSource busyCts;

        [ObservableProperty]
        private string titleName = Globals.Assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;

        [ObservableProperty]
        private string titleVersion = $"v{Globals.Assembly.GetName().Version}";

        [ObservableProperty]
        private Window instance;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PackCommand))]
        private string originalPicturePath = null;

        [ObservableProperty]
        private string asinList = null;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PackCommand))]
        private int asinCount;

        [ObservableProperty]
        private BindingList<string> amazonPictureCodes = [.. Constants.amazonImageCodes];

        [ObservableProperty]
        private string selectedAmazonPictureCode = Constants.amazonImageCodes[0];

        [ObservableProperty]
        private string status = "Ready";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PackCommand))]
        [NotifyCanExecuteChangedFor(nameof(AbortCommand))]
        private bool isBusy;

        [ObservableProperty]
        private int progressBarValue;

        [ObservableProperty]
        private int progressBarMaximum;

        [ObservableProperty]
        private int fileToPackInZip = 200;

        partial void OnAsinListChanged(string value)
        {
            if (value == null)
            {
                this.asins.Clear();
                this.AsinCount = 0;
                return;
            }

            this.asins.Clear();
            this.asins.AddRange(value.Replace("\r", "").Split('\n').Where(x => !string.IsNullOrEmpty(x) && x.Length == 10));
            this.AsinCount = this.asins.Count;
        }

        #region Ctor
        public MainWindowViewModel()
        {

        }
        #endregion

        private bool CanExecutePack()
        {
            if (this.IsBusy)
            {
                return false;
            }

            if (string.IsNullOrEmpty(this.OriginalPicturePath) || !File.Exists(this.OriginalPicturePath))
            {
                return false;
            }

            if (this.asins.Count <= 0)
            {
                return false;
            }

            return true;
        }

        [RelayCommand]
        private async Task Browse()
        {
            IReadOnlyList<IStorageFile> result = await this.Instance.StorageProvider.OpenFilePickerAsync(new()
            {
                Title = "Choose a picture",
                AllowMultiple = false,
                SuggestedFileName = "picture.png",
                FileTypeFilter = [FilePickerFileTypes.ImageAll]
            });

            if (result.Count <= 0)
            {
                return;
            }

            string filepath = result[0].Path.LocalPath;

            if (!File.Exists(filepath))
            {
                return;
            }

            this.OriginalPicturePath = filepath;
        }

        [RelayCommand(CanExecute = nameof(CanExecuteAbort))]
        private void Abort()
        {
            this.busyCts?.Cancel();
        }

        private bool CanExecuteAbort()
        {
            return this.IsBusy;
        }

        [RelayCommand(CanExecute = nameof(CanExecutePack))]
        private async Task Pack()
        {
            this.busyCts = new();

            this.IsBusy = true;
            this.ProgressBarValue = 0;
            this.ProgressBarMaximum = this.asins.Count;

            string dir = Path.Combine(AppContext.BaseDirectory, "tmp");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string imagefile = Path.Combine(dir, Path.GetFileName(this.OriginalPicturePath));

            this.Status = "Copying...";

            await Task.Run(() => File.Copy(this.OriginalPicturePath, imagefile, true));

            if (!File.Exists(Path.Combine(dir, imagefile)))
            {
                this.Status = "Error copying";
            }

            this.Status = "File copied to temp";

            string copypath = Path.Combine(dir, "files");

            if (!Directory.Exists(copypath))
            {
                Directory.CreateDirectory(copypath);
            }

            foreach (string a in this.asins.Select(x => x.ToUpper()))
            {
                await Task.Run(() => File.Copy(imagefile, Path.Combine(copypath, $"{a}.{this.SelectedAmazonPictureCode}.{Path.GetExtension(this.OriginalPicturePath).Replace(".","")}"), true));
            }

            string readyPath = Path.Combine(AppContext.BaseDirectory, "ready");

            if (!Directory.Exists(readyPath))
            {
                Directory.CreateDirectory(readyPath);
            }

            this.Status = "Processing...";

            await Task.Run(() =>
            {
                int currentFilesPacked = 0;
                int zipNumber = 1;
                int zipNumberFilename = 1;

                for (int i = 0; i < (int)Math.Ceiling(Directory.GetFiles(copypath).Length / (float)this.FileToPackInZip); i++)
                {
                    string zipPath = Path.Combine(readyPath, $"pack{zipNumberFilename:000}.zip");

                    while (File.Exists(zipPath) && !this.busyCts.Token.IsCancellationRequested)
                    {
                        zipPath = Path.Combine(readyPath, $"pack{zipNumberFilename:000}.zip");
                        zipNumberFilename++;
                    }

                    using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                    {
                        foreach (string asinImage in Directory.GetFiles(copypath).Skip((zipNumber - 1) * this.FileToPackInZip).Take(this.FileToPackInZip))
                        {
                            if (this.busyCts.Token.IsCancellationRequested)
                            {
                                break;
                            }

                            zip.CreateEntryFromFile(asinImage, Path.GetFileName(asinImage));
                            currentFilesPacked++;

                            this.Status = $"Processing [{currentFilesPacked}/{this.asins.Count}]";
                            this.ProgressBarValue = currentFilesPacked;
                        }
                        zipNumber++;
                        zipNumberFilename++;
                    }
                }
            }, this.busyCts.Token);

            this.Status = "Cleaning...";

            await Task.Run(() => Directory.Delete(dir, true));

            if (this.busyCts.Token.IsCancellationRequested)
            {
                this.Status = "Aborted";
                this.IsBusy = false;
                this.busyCts?.Dispose();
                return;
            }

            this.Status = $"Ready - processed {this.AsinCount} files.";

            this.AsinList = null;

            Process.Start(new ProcessStartInfo
            {
                FileName = readyPath,
                UseShellExecute = true,
                Verb = "open"
            });

            this.IsBusy = false;
            this.busyCts?.Dispose();
        }
    }
}
