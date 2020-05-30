using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Threading.Tasks;

namespace nlptextdoc.image2
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

        internal static (int width, int height) GetViewDimensions(WebView2 webview)
        {
            return ((int)webview.ActualWidth, (int)webview.ActualHeight);
        }

        internal static void SetViewDimensions(WebView2 webview, (int width, int height) dims)
        {
            webview.Width = dims.width;
            if (dims.height > 0) webview.Height = dims.height;
        }

        internal static async Task<(int width, int height)> GetContentDimensionsAsync(CoreWebView2 webview)
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

        internal static async Task<string> CreateAndSaveScreenshotAsync(CoreWebView2 webview, string fileName, string nameSuffix = "screen")
        {
            // Capture and save screenshot to disk
            var (filePath, stream) = FilesManager.GetStreamToWriteImage(fileName + "_" + nameSuffix + ".png");
            using (stream)
            {
                await webview.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
            }
            return filePath;
        }

        internal static async Task<PageElement> CreateAndSaveTextBoundingBoxes(CoreWebView2 webview, string fileName)
        {
            // Extract json description of all text bounding boxes
            await JavascriptInterop.InjectJavascriptDefinitionsAsync(webview);    
            var textBoundingBoxes = await JavascriptInterop.ExtractTextAsJson(webview, false);
            if (textBoundingBoxes.StartsWith("ERROR:"))
            {
                throw new Exception("Javascript error : " + textBoundingBoxes.Substring(6));
            }

            // Write json description to disk
            FilesManager.WriteTextToFile(fileName + "_boxes.json", textBoundingBoxes);

            // Return .NET object tree version
            return JavascriptInterop.ConvertJsonToPageElements(textBoundingBoxes);
        }
    }
}
