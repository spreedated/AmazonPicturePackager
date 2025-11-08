using AmazonPicturePackager.Logic;
using Avalonia;
using Microsoft.Extensions.Logging;
using neXn.Lib.ConfigurationHandler;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AmazonPicturePackager
{
    internal static class Program
    {
        private readonly static LogEventLevel minimumLevel = LogEventLevel.Verbose;

        public static string AppLocalBasePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "neXn-Systems", "AmazonPictureManager");

        [STAThread]
        public static async Task Main(string[] args)
        {
            // Setup logger
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console(restrictedToMinimumLevel: minimumLevel)
            .WriteTo.Debug()
            .Enrich.WithProperty("Application", typeof(Program).Assembly.GetName().Name)
            .CreateLogger();

            Microsoft.Extensions.Logging.ILogger logger = new SerilogLoggerProvider().CreateLogger("app");

            logger.LogInformation("Starting up");

            // Load Config
            string userConfigPath = Path.Combine(AppLocalBasePath, "config", "user-settings.json");

            Globals.UserConfig = new ConfigurationHandler<Models.Configuration>(new(userConfigPath));
            await Globals.UserConfig.Load().ConfigureAwait(false);
            logger.LogInformation("Loaded user config");

            logger.LogTrace("Loading/building app...");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                        .UsePlatformDetect()
                        .WithInterFont()
                        .LogToTrace();
        }
    }
}
