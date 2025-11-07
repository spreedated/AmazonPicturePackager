namespace AmazonPicturePackager.Models
{
    public sealed record Configuration
    {
        public int LastUsedImageCode { get; set; }
        public int LastUsedFilesPerZip { get; set; } = 200;
    }
}
