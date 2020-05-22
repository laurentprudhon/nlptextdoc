using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace nlptextdoc.image
{
    class ScreenCapture
    {
        static Random rndgen = new Random();

        internal static int GetRandowWidth()
        {
            int random = rndgen.Next(100);
            // Market shares France April 2020
            // excluding 300-400 feature phones resolutions
            if (random < 7) return 768;
            if (random < 11) return 1024;
            if (random < 24) return 1280;
            if (random < 43) return 1366;
            if (random < 54) return 1440;
            if (random < 63) return 1536;
            if (random < 72) return 1600;
            if (random < 75) return 1680;
            /*if (random < 100)*/ return 1920;
        }

        internal static (int width, int height) GetViewDimensions(WebView webview)
        {
            return ((int)webview.ActualWidth, (int)webview.ActualHeight);
        }

        internal static void SetViewDimensions(WebView webview, (int width, int height) dims)
        {
            webview.Width = dims.width;
            if (dims.height > 0) webview.Height = dims.height;
        }

        internal static async Task<(int width, int height)> GetContentDimensionsAsync(WebView webview)
        {
            int contentWidth, contentHeight;

            var widthString = await JavascriptInterop.ExecuteJavascriptCodeAsync(webview, "document.body.scrollWidth.toString()");
            if (!int.TryParse(widthString, out contentWidth))
                throw new Exception(string.Format("failure/width:{0}", widthString));

            var heightString = await JavascriptInterop.ExecuteJavascriptCodeAsync(webview, "document.body.scrollHeight.toString()");
            if (!int.TryParse(heightString, out contentHeight))
                throw new Exception(string.Format("failure/height:{0}", heightString));

            return (contentWidth, contentHeight);
        }

        internal static async Task CreateAndSaveScreenshotAsync(WebView webview, Rectangle screenshot, string fileName, string nameSuffix = "screen", bool warmup = false)
        {
            // Capture picture in memory
            var brush = new WebViewBrush();
            brush.Stretch = Stretch.Uniform;
            brush.AlignmentY = AlignmentY.Top;
            brush.SetSource(webview);
            brush.Redraw();

            // Display in Rectangle
            screenshot.Width = webview.Width;
            screenshot.Height = webview.Height;
            screenshot.Fill = brush;

            // Get Rectangle pixels
            RenderTargetBitmap rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(screenshot);
            var buffer = await rtb.GetPixelsAsync();

            // Write pixels to disk
            if (!warmup)
            {
                await FilesManager.WriteImageToFileAsync(fileName + "_" + nameSuffix + ".png", rtb.PixelWidth, rtb.PixelHeight, buffer.ToArray());
            }
        }

        internal static async Task<PageElement> CreateAndSaveTextBoundingBoxes(WebView webview, string fileName)
        {
            // Extraction json description of all text bounding boxes
            await JavascriptInterop.InjectJavascriptDefinitionsAsync(webview);
            var textBoundingBoxes = await JavascriptInterop.ExtractTextAsJson(webview, true);
            if(textBoundingBoxes.StartsWith("ERROR:"))
            {
                throw new Exception("Javascript error : " + textBoundingBoxes.Substring(6));
            }

            // Write json description to disk
            await FilesManager.WriteTextToFileAsync(fileName + "_boxes.json", textBoundingBoxes);

            // Return .NET object tree version
            return JavascriptInterop.ConvertJsonToPageElements(textBoundingBoxes);
        }
    }
}
