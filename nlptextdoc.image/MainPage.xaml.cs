using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

// Pour plus d'informations sur le modèle d'élément Page vierge, consultez la page https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace nlptextdoc.image
{
    /// <summary>
    /// Une page vide peut être utilisée seule ou constituer une page de destination au sein d'un frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            urlbox.Text = "https://www.creditmutuel.fr/fr/particuliers.html";
        }

        private void NavigateToUrl(object sender, RoutedEventArgs e)
        {
            webview.Navigate(new Uri(urlbox.Text));
        }

        private async void ExecuteJavascript(object sender, RoutedEventArgs e)
        {
            JavascriptInterop.InjectJavascriptDefinitions(webview);
            result.Text = await JavascriptInterop.ExtractTextAsJson(webview, true);
            var pageTree = JavascriptInterop.ConvertJsonToPageElements(result.Text);
        }

        private async void CaptureSnapshot(object sender, RoutedEventArgs e)
        {
            // Get content properties
            double originalWidth, originalHeight;
            int contentWidth, contentHeight;

            originalWidth = webview.Width;
            var widthString = await JavascriptInterop.ExecuteJavascriptCode(webview, "document.body.scrollWidth.toString()");
            if (!int.TryParse(widthString, out contentWidth))
                throw new Exception(string.Format("failure/width:{0}", widthString));

            originalHeight = webview.Height;
            var heightString = await JavascriptInterop.ExecuteJavascriptCode(webview, "document.body.scrollHeight.toString()");
            if (!int.TryParse(heightString, out contentHeight))
                throw new Exception(string.Format("failure/height:{0}", heightString));

            var originalVisibilty = webview.Visibility;

            // Resize to content size
            webview.Width = contentWidth;
            webview.Height = contentHeight;
            webview.Visibility = Visibility.Visible;

            // Capture picture in memory
            //InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
            //await webview.CapturePreviewToStreamAsync(stream);
            var brush = new WebViewBrush();
            brush.Stretch = Stretch.Uniform;
            brush.AlignmentY = AlignmentY.Top;
            brush.SetSource(webview);
            brush.Redraw();

            Thread.Sleep(200);

            // Display in Rectangle
            capture.Width = contentWidth;
            capture.Height = contentHeight;
            capture.Fill = brush;

            /*// Reset to original size
            webview.Width = originalWidth;
            webview.Height = originalHeight;
            webview.Visibility = originalVisibilty;*/

            Thread.Sleep(200);

            // Save image
            RenderTargetBitmap rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(capture);
            var buffer = await rtb.GetPixelsAsync();

            FilesManager.WriteImageToFile("capture.png", (uint)contentWidth, (uint)contentHeight, buffer.ToArray());

            // Capture description
            JavascriptInterop.InjectJavascriptDefinitions(webview);
            var description = await JavascriptInterop.ExtractTextAsJson(webview, true);

            FilesManager.WriteTextToFile("capture.txt", description);

            Thread.Sleep(1000);

            // Save image with debug info
            rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(capture);
            buffer = await rtb.GetPixelsAsync();

            FilesManager.WriteImageToFile("capture_rects.png", (uint)contentWidth, (uint)contentHeight, buffer.ToArray());
        }

        static async Task<SoftwareBitmap> CreateScaledBitmapFromStreamAsync(int width, int height, IRandomAccessStream source)
        {
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(source);
            BitmapTransform transform = new BitmapTransform();
            transform.ScaledHeight = (uint)height;
            transform.ScaledWidth = (uint)width;
            var bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.DoNotColorManage);
            return bitmap;
        }
    }
}
