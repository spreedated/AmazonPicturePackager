using System.Reflection;

namespace AmazonPicturePackager.Logic
{
    internal static class Globals
    {
        public static Assembly Assembly { get; } = typeof(Globals).Assembly;
    }
}
