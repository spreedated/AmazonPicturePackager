using System.Collections.Immutable;

namespace AmazonPicturePackager.Logic
{
    internal static class Constants
    {
        public readonly static ImmutableArray<string> amazonImageCodes = [
                                                            // Primary product image
                                                            "MAIN",
                                                            // Additional product images
                                                            "PT01", "PT02", "PT03", "PT04", "PT05",
                                                            // Lifestyle images
                                                            "LC01", "LC02", "LC03",
                                                            // Infographics & Close-Ups
                                                            "IN01", "IN02",
                                                            // Swatch image
                                                            "SWCH",
                                                            // Product specification images
                                                            "PS01", "PS02",
                                                            // Certification or compliance images
                                                            "CE01", "CE02",
                                                            // Feature images
                                                            "FL01", "FL02",
                                                            // Packaging images
                                                            "PK01", "PK02",
                                                            // White background images
                                                            "WB01", "WB02"
                                                        ];
    }
}
