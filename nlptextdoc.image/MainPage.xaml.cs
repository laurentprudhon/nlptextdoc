using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace nlptextdoc.image
{
    public sealed partial class MainPage : Page
    {
        // -- Configuration --
        const string DATASET = "Banque";
        const int STARTCOUNTER = 1;

        public MainPage()
        {
            this.InitializeComponent();

            // Initialize URLs list and navigate to next URL
            urlsToCapture = URLsSource.ReadDatasetURLs(DATASET).GetEnumerator();
            if (STARTCOUNTER > 0)
            {
                counter = STARTCOUNTER - 1;
                for (int i = 0; i < counter; i++) urlsToCapture.MoveNext();
            }
            NavigateToNextUrl(this, null);
        }

        // Internal state
        IEnumerator<string> urlsToCapture;
        int counter = 0;
        string currentURL;

        private void NavigateToNextUrl(object sender, RoutedEventArgs e)
        {
            if (urlsToCapture.MoveNext())
            {
                counter++;
                currentURL = urlsToCapture.Current;

                counterView.Text = counter.ToString();
                urlView.Text = currentURL;
                RefreshCurrentUrl(sender, e);
            }
            else
            {
                Application.Current.Exit();
            }
        }

        private void RefreshCurrentUrl(object sender, RoutedEventArgs e)
        {
            webview.Navigate(new Uri(currentURL));
        }

        private async void CaptureScreenshots(object sender, RoutedEventArgs e)
        {
            await DoCaptureScreenshots();
        }

        private async void CaptureScreenshotsAndNavigateToNextURL(object sender, RoutedEventArgs e)
        {
            await DoCaptureScreenshots();
            NavigateToNextUrl(sender, e);
        }

        private async Task DoCaptureScreenshots()
        {
            try
            {
                // Get view and content dimensions
                var viewDimensions = ScreenCapture.GetViewDimensions(webview);
                var contentDimensions = await ScreenCapture.GetContentDimensionsAsync(webview);

                // Resize view to content size
                ScreenCapture.SetViewDimensions(webview, contentDimensions);

                // Get unique file name for the current URL
                var fileName = await JavascriptInterop.GetUniqueFileNameFromURLAsync(webview);
                fileName = counter.ToString("D5") + "_" + fileName;

                // Capture a screenshot
                await ScreenCapture.CreateAndSaveScreenshotAsync(webview, capture, fileName);

                // Capture a description of all chars/words/lines/blocks bounding boxes
                // Draw all these bounding boxes on the screen
                var pageElementsTree = await ScreenCapture.CreateAndSaveTextBoundingBoxes(webview, fileName);

                // Capture a new screenshot
                await ScreenCapture.CreateAndSaveScreenshotAsync(webview, captureBoxes, fileName, "boxes");

                // Reset view to its original size
                ScreenCapture.SetViewDimensions(webview, viewDimensions);

                // Generate masks for training
                //await MaskGenerator.GenerateMasks(fileName, contentDimensions, pageElementsTree);
            }
            catch(Exception e)
            {
                var md = new MessageDialog(e.Message);
                await md.ShowAsync();
            }
        }
    }
}
