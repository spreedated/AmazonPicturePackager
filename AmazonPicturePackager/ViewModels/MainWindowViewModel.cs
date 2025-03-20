using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AmazonPicturePackager.Logic;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AmazonPicturePackager.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly List<string> asins = [];

        [ObservableProperty]
        private string titleName = Globals.Assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;

        [ObservableProperty]
        private string titleVersion = $"v{Globals.Assembly.GetName().Version}";

        [ObservableProperty]
        private Window instance;

        [ObservableProperty]
        private string originalPicturePath = null;

        [ObservableProperty]
        private string asinList = null;

        [ObservableProperty]
        private int asinCount;

        partial void OnAsinListChanged(string value)
        {
            this.asins.Clear();
            this.asins.AddRange(value.Split('\n').Where(x => !string.IsNullOrEmpty(x) && x.Length == 10));
            this.AsinCount = this.asins.Count;
        }

        #region Ctor
        public MainWindowViewModel()
        {

        }
        #endregion

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

            this.OriginalPicturePath = result[0].Path.ToString();
        }
    }
}
