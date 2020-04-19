using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using System.Threading.Tasks;

namespace nlptextdoc.image
{
    class MaskGenerator
    {
        internal static async Task GenerateMasks(string fileName, (int width, int height) dimensions, PageElement pageElementsTree)
        {
            // Mask for text blocks
            using (var mask = new Image<Gray8>(dimensions.width, dimensions.height))
            {
                GenerateBoundingBoxes(BoundingBoxType.TextBlock, mask, pageElementsTree.children);
                await FilesManager.WriteImageToFileAsync(fileName + "_blocks.png", mask);
            }

            // Mask for text lines
            using (var mask = new Image<Gray8>(dimensions.width, dimensions.height))
            {
                GenerateBoundingBoxes(BoundingBoxType.TextLine, mask, pageElementsTree.children);
                await FilesManager.WriteImageToFileAsync(fileName + "_lines.png", mask);
            }

            // Mask for words
            using (var mask = new Image<Gray8>(dimensions.width, dimensions.height))
            {
                GenerateBoundingBoxes(BoundingBoxType.Word, mask, pageElementsTree.children);
                await FilesManager.WriteImageToFileAsync(fileName + "_words.png", mask);
            }

            // Mask for chars
            using (var mask = new Image<Gray8>(dimensions.width, dimensions.height))
            {
                GenerateBoundingBoxes(BoundingBoxType.Char, mask, pageElementsTree.children);
                await FilesManager.WriteImageToFileAsync(fileName + "_chars.png", mask, PngBitDepth.Bit8);
            }
        }

        private static void GenerateBoundingBoxes(BoundingBoxType boxType, Image<Gray8> mask, PageElement[] pageElements)
        {
            foreach (var pageElement in pageElements)
            {
                if (pageElement.IsTextBlock || pageElement.IsTextLabel) {
                    if (boxType == BoundingBoxType.TextBlock)
                    {
                        DrawBoundingBox(mask, pageElement.boundingBox, Color.White);
                    }
                    else if (pageElement.IsTextBlock)
                    {
                        if (boxType == BoundingBoxType.TextLine)
                        {
                            foreach(var line in pageElement.lines)
                            {
                                DrawBoundingBox(mask, line.boundingBox, Color.White);
                            }
                        }
                        else if (boxType == BoundingBoxType.Word)
                        {
                            foreach (var line in pageElement.lines)
                            {
                                foreach (var word in line.words)
                                {
                                    DrawBoundingBox(mask, word.boundingBox, Color.White);
                                }
                            }
                        }
                        else if (boxType == BoundingBoxType.Char)
                        {
                            foreach (var line in pageElement.lines)
                            {
                                foreach (var word in line.words)
                                {
                                    foreach (var letter in word.letters)
                                    {
                                        DrawBoundingBox(mask, letter.boundingBox, GetCharColor(letter.@char));
                                    }
                                }
                            }
                        }
                    }
                }
                if(pageElement.children != null && pageElement.children.Length > 0)
                {
                    GenerateBoundingBoxes(boxType, mask, pageElement.children);
                }
            }
        }

        private static Color GetCharColor(char @char)
        {
            byte luminance = (byte)(@char % 256); 
            return Color.FromRgb(luminance, luminance, luminance);
        }

        private static void DrawBoundingBox(Image<Gray8> mask, BoundingBox boundingBox, Color color)
        {
            mask.Mutate(m => m.Fill(color, new RectangleF(boundingBox.x, boundingBox.y, boundingBox.width, boundingBox.height)));
        }
    }
}
