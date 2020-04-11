using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

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
            scriptbox.Text = "extractText(true)";
        }

        private void NavigateToUrl(object sender, RoutedEventArgs e)
        {
            webview.Navigate(new Uri(urlbox.Text));
        }

        private async void ExecuteJavascript(object sender, RoutedEventArgs e)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "GenerateTextImagesUWP.extracttext2.js";
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                var reader = new StreamReader(stream);
                string javascript = reader.ReadToEnd();
                await webview.InvokeScriptAsync("eval", new[] { javascript });
            }

            result.Text = await webview.InvokeScriptAsync("eval", new[] { scriptbox.Text });
        }

        private async void CaptureSnapshot(object sender, RoutedEventArgs e)
        {
            // Get content properties
            double originalWidth, originalHeight;
            int contentWidth, contentHeight;

            originalWidth = webview.Width;
            var widthString = await webview.InvokeScriptAsync("eval", new[] { "document.body.scrollWidth.toString()" });
            if (!int.TryParse(widthString, out contentWidth))
                throw new Exception(string.Format("failure/width:{0}", widthString));

            originalHeight = webview.Height;
            var heightString = await webview.InvokeScriptAsync("eval", new[] { "document.body.scrollHeight.toString()" });
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

            // Save to file
            RenderTargetBitmap rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(capture);
            var buffer = await rtb.GetPixelsAsync();

            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("capture.png", CreationCollisionOption.ReplaceExisting);
            /*var propertySet = new Windows.Graphics.Imaging.BitmapPropertySet();
            var qualityValue = new Windows.Graphics.Imaging.BitmapTypedValue(
                1.0, // Maximum quality
                Windows.Foundation.PropertyType.Single
                );
            propertySet.Add("ImageQuality", qualityValue);*/
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, await file.OpenAsync(FileAccessMode.ReadWrite)/*, propertySet*/);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                (uint)contentWidth,
                (uint)contentHeight,
                DisplayInformation.GetForCurrentView().LogicalDpi,
                DisplayInformation.GetForCurrentView().LogicalDpi,
                buffer.ToArray());
            await encoder.FlushAsync();

            // Capture descriptions

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "nlptextdoc.image.extracttext.js";
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                var reader = new StreamReader(stream);
                string javascript = reader.ReadToEnd();
                await webview.InvokeScriptAsync("eval", new[] { javascript });
            }
            var description = await webview.InvokeScriptAsync("eval", new[] { "extractText()" });

            var textFile = await ApplicationData.Current.LocalFolder.CreateFileAsync("capture.txt", CreationCollisionOption.ReplaceExisting);
            using (var stream = await textFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                using (IOutputStream outputStream = stream.GetOutputStreamAt(0))
                {
                    using (DataWriter dataWriter = new DataWriter(outputStream))
                    {
                        dataWriter.WriteString(description);
                        await dataWriter.StoreAsync();
                        dataWriter.DetachStream();
                    }
                    await outputStream.FlushAsync();
                }
            }
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
