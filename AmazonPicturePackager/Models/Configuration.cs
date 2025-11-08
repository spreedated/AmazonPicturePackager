namespace AmazonPicturePackager.Models
{
    public sealed record Configuration
    {
        public string LastUsedImageCode { get; set; }
        public int LastUsedFilesPerZip { get; set; } = 200;
        public bool OpenOutputFolderWhenDone { get; set; } = true;
    }
}
