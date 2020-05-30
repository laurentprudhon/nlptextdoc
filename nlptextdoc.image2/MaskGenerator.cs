using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;

namespace nlptextdoc.image2
{
    class MaskGenerator
    {
        internal static string DrawBoundingBoxes(string inputFilePath, PageElement pageElementsTree)
        {
            // Mask for text blocks
            using (var image = Image.Load(inputFilePath))
            {
                DrawBoundingBoxes(image, pageElementsTree.children);

                FileInfo file = new FileInfo(inputFilePath);
                var outputFilePath = Path.Combine(file.DirectoryName, file.Name.Substring(0,file.Name.Length-10) + "_boxes.png");
                image.Save(outputFilePath);

                return outputFilePath;
            }
        }

        private static int altLetter = 0;

        private static void DrawBoundingBoxes(Image image, PageElement[] pageElements)
        {
            foreach (var pageElement in pageElements)
            {
                if (pageElement.IsTextBlock)
                {
                    foreach (var line in pageElement.lines)
                    {
                        foreach (var word in line.words)
                        {
                            foreach (var letter in word.letters)
                            {
                                DrawBoundingBox(image, letter.boundingBox, null, (altLetter % 2 == 0 ? Color.FromRgb(170,170,255) : Color.FromRgb(85,85,255)).WithAlpha(0.4f));
                                altLetter++;
                            }
                            DrawBoundingBox(image, word.boundingBox, Color.Blue);
                        }
                        DrawBoundingBox(image, line.boundingBox, Color.Green);
                    }
                    DrawBoundingBox(image, pageElement.boundingBox, Color.Red);
                }
                else if (pageElement.IsTextLabel)
                {
                    DrawBoundingBox(image, pageElement.boundingBox, Color.Orange, Color.Yellow.WithAlpha(0.4f));
                }
                if (pageElement.children != null && pageElement.children.Length > 0)
                {
                    DrawBoundingBoxes(image, pageElement.children);
                }
            }
        }

        private static void DrawBoundingBox(Image image, BoundingBox boundingBox, Color? drawColor, Color? fillColor = null)
        {
            // Temporary fix for a Javascript bug I can't reproduce
            if (boundingBox.width <= 0) boundingBox.width = 8;

            try
            {
                if (fillColor.HasValue)
                {
                    image.Mutate(m => m.Fill(fillColor.Value, new RectangleF(boundingBox.x, boundingBox.y, boundingBox.width, boundingBox.height)));
                }
                if (drawColor.HasValue)
                {
                    image.Mutate(m => m.Draw(drawColor.Value, 1, new RectangleF(boundingBox.x, boundingBox.y, boundingBox.width, boundingBox.height)));
                }
            }
            catch(Exception e)
            {
                throw e;
            }
        }
    }
}
