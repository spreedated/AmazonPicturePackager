using AmazonPicturePackager.Logic;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neXn.Lib;
using neXn.Ui.Animation;
using Serilog.Extensions.Logging;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
        [NotifyCanExecuteChangedFor(nameof(ClearAsinListCommand))]
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
        private int fileToPackInZip;

        [ObservableProperty]
        private bool openOutputFolderWhenDone;

        partial void OnOpenOutputFolderWhenDoneChanged(bool value)
        {
            Globals.UserConfig.RuntimeConfiguration.OpenOutputFolderWhenDone = value;
            Task.Run(Globals.UserConfig.Save);
        }

        partial void OnSelectedAmazonPictureCodeChanged(string value)
        {
            Globals.UserConfig.RuntimeConfiguration.LastUsedImageCode = value;
            Task.Run(Globals.UserConfig.Save);
        }

        partial void OnFileToPackInZipChanged(int value)
        {
            Globals.UserConfig.RuntimeConfiguration.LastUsedFilesPerZip = value;
            Task.Run(Globals.UserConfig.Save);
        }

        partial void OnAsinListChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
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
                this.SelectedAmazonPictureCode = Constants.amazonImageCodes.IndexOf(Globals.UserConfig.RuntimeConfiguration.LastUsedImageCode) != -1 ? Globals.UserConfig.RuntimeConfiguration.LastUsedImageCode : Constants.amazonImageCodes[0];
                this.FileToPackInZip = Globals.UserConfig.RuntimeConfiguration.LastUsedFilesPerZip;
                this.OpenOutputFolderWhenDone = Globals.UserConfig.RuntimeConfiguration.OpenOutputFolderWhenDone;
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

        [RelayCommand(CanExecute = nameof(CanExecuteClearAsinList))]
        private void ClearAsinList()
        {
            this.AsinList = null;
        }

        private bool CanExecuteClearAsinList()
        {
            return !string.IsNullOrEmpty(this.AsinList);
        }

        [RelayCommand(CanExecute = nameof(CanExecutePack))]
        private async Task Pack()
        {
            this.busyCts = new();

            Globals.AppStatus.Change("Processing...", true);
            this.ProgressBarValue = 0;
            this.ProgressBarMaximum = this.asins.Count;

            ILogger l = new SerilogLoggerProvider().CreateLogger("Processor.Packager");

            Processor.Packager p = new(Program.AppLocalBasePath, l);

            p.CurrentPackedFilesCountChanged += (s, e) =>
            {
                this.ProgressBarValue = e;
            };

            if (!await p.PackAsync(this.OriginalPicturePath, this.AsinList.Split('\n'), this.SelectedAmazonPictureCode, this.FileToPackInZip, this.OpenOutputFolderWhenDone, this.busyCts.Token))
            {
                Globals.AppStatus.Change("Aborted", false, true);
                return;
            }

            Globals.AppStatus.Change($"Ready - processed {this.AsinCount} files.", true);

            Globals.AppStatus.SetDefaultStatus();
            this.busyCts?.Dispose();
        }
    }
}
