using AmazonPicturePackager.Logic;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neXn.Lib;
using neXn.Ui.Animation;
using Serilog.Extensions.Logging;
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

namespace AmazonPicturePackager.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly ILogger logger = new SerilogLoggerProvider().CreateLogger("MainWindowViewModel");
        private readonly TextWaitingAnimation textWaitingAnimation;
        private readonly List<string> asins = [];
        private CancellationTokenSource busyCts;

        [ObservableProperty]
        private string titleName = Globals.Assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;

        [ObservableProperty]
        private string titleVersion = $"v{Globals.Assembly.GetName().Version.ToNiceString()}";

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
        private string selectedAmazonPictureCode;

        [ObservableProperty]
        private string status = "Ready";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PackCommand))]
        [NotifyCanExecuteChangedFor(nameof(AbortCommand))]
        private bool isBusy;

        [ObservableProperty]
        private int progressBarValue = 100;

        [ObservableProperty]
        private int progressBarMaximum;

        [ObservableProperty]
        private int fileToPackInZip = 200;

        partial void OnSelectedAmazonPictureCodeChanged(string value)
        {
            Globals.UserConfig.RuntimeConfiguration.LastUsedImageCode = Array.IndexOf(Constants.amazonImageCodes, value);
            Task.Run(Globals.UserConfig.Save);
        }

        partial void OnFileToPackInZipChanged(int value)
        {
            Globals.UserConfig.RuntimeConfiguration.LastUsedFilesPerZip = value;
            Task.Run(Globals.UserConfig.Save);
        }

        partial void OnAsinListChanged(string value)
        {
            if (value == null)
            {
                this.asins.Clear();
                this.AsinCount = default;
                return;
            }

            this.asins.Clear();
            this.asins.AddRange(value.Replace("\r", "").Split('\n').Where(x => !string.IsNullOrEmpty(x) && x.Length == 10).Distinct());
            this.AsinCount = this.asins.Count;

            this.logger.LogTrace("ASIN list changed, total valid & distinct ASINs: {AsinCount}", this.AsinCount);
        }

        #region Ctor
        public MainWindowViewModel()
        {
            Globals.AppStatus.StatusChanged += (s, e) =>
            {
                this.IsBusy = Globals.AppStatus.IsBusy;
            };

            this.textWaitingAnimation = new()
            {
                UseBrackets = true,
                AnimationType = TextWaitingAnimation.AnimationTypes.BlockChars,
                Interval = 400
            };

            this.LoadUserConfigSettingsToViewModel();

            this.textWaitingAnimation.AnimationChanged += this.TextWaitingAnimation_AnimationChanged;
            this.textWaitingAnimation.Start();
        }
        #endregion

        private void LoadUserConfigSettingsToViewModel()
        {
            if (Globals.UserConfig != null)
            {
                this.SelectedAmazonPictureCode = Constants.amazonImageCodes[Globals.UserConfig.RuntimeConfiguration.LastUsedImageCode];
                this.FileToPackInZip = Globals.UserConfig.RuntimeConfiguration.LastUsedFilesPerZip;
            }
        }

        private void TextWaitingAnimation_AnimationChanged(object sender, string e)
        {
            this.Status = $"{e} {Globals.AppStatus.Status}";
        }

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

            Globals.AppStatus.Change("Processing...", true);
            this.ProgressBarValue = 0;
            this.ProgressBarMaximum = this.asins.Count;

            string dir = Path.Combine(Program.AppLocalBasePath, "temp");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string imagefile = Path.Combine(dir, Path.GetFileName(this.OriginalPicturePath));

            Globals.AppStatus.Change("Copying...", true);

            await Task.Run(() => File.Copy(this.OriginalPicturePath, imagefile, true));

            if (!File.Exists(Path.Combine(dir, imagefile)))
            {
                Globals.AppStatus.Change("Error copying", true);
            }

            Globals.AppStatus.Change("File copied to temp", true);

            string copypath = Path.Combine(dir, "files");

            if (!Directory.Exists(copypath))
            {
                Directory.CreateDirectory(copypath);
            }

            foreach (string a in this.asins.Select(x => x.ToUpper()))
            {
                await Task.Run(() => File.Copy(imagefile, Path.Combine(copypath, $"{a}.{this.SelectedAmazonPictureCode}.{Path.GetExtension(this.OriginalPicturePath).Replace(".", "")}"), true));
            }

            string readyPath = Path.Combine(Program.AppLocalBasePath, "ready");

            if (!Directory.Exists(readyPath))
            {
                Directory.CreateDirectory(readyPath);
            }

            Globals.AppStatus.Change("Processing...", true);

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

                            Globals.AppStatus.Change($"Processing [{currentFilesPacked}/{this.asins.Count}]", true);
                            this.ProgressBarValue = currentFilesPacked;
                        }
                        zipNumber++;
                        zipNumberFilename++;
                    }
                }
            }, this.busyCts.Token);

            Globals.AppStatus.Change("Cleaning...", true);

            await Task.Run(() => Directory.Delete(dir, true));

            if (this.busyCts.Token.IsCancellationRequested)
            {
                Globals.AppStatus.Change("Aborted", false, true);
                this.busyCts?.Dispose();
                return;
            }

            Globals.AppStatus.Change($"Ready - processed {this.AsinCount} files.", true);

            this.AsinList = null;

            Process.Start(new ProcessStartInfo
            {
                FileName = readyPath,
                UseShellExecute = true,
                Verb = "open"
            });

            Globals.AppStatus.SetDefaultStatus();
            this.busyCts?.Dispose();
        }
    }
}
