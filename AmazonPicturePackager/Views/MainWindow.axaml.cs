using AmazonPicturePackager.ViewModels;
using Avalonia.Controls;

namespace AmazonPicturePackager.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            this.DataContext = new MainWindowViewModel();
            ((MainWindowViewModel)this.DataContext).Instance = this;
        }
    }
}