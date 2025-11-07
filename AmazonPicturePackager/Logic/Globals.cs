using AmazonPicturePackager.Models;
using neXn.Lib.ConfigurationHandler;
using neXn.Ui;
using System.Reflection;

namespace AmazonPicturePackager.Logic
{
    internal static class Globals
    {
        public static Assembly Assembly { get; } = typeof(Globals).Assembly;
        public static AppStatus<string> AppStatus { get; } = new("System ready");
        public static ConfigurationHandler<Configuration> UserConfig { get; set; }
    }
}
