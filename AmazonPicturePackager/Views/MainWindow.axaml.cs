using AmazonPicturePackager.ViewModels;
using Avalonia.Controls;

namespace AmazonPicturePackager.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            ((MainWindowViewModel)this.DataContext).Instance = this;
        }
    }
}