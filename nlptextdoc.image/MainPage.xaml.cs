using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
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

            // Output folder link => clipboard
            var outputPath = FilesManager.GetOutputFolderPath();
            var cbData = new DataPackage();
            cbData.SetText(outputPath);
            Clipboard.SetContent(cbData);

            // Initialize URLs list and navigate to next URL
            urlsToCapture = URLsSource.ReadDatasetURLs(DATASET).ToList();
            counter = STARTCOUNTER - 1;
            if(STARTCOUNTER == 1)
            {
                counter = FilesManager.GetLastSavedCounter() + 1;
            }
            NavigateToNextUrl(this, null);
        }

        // Internal state
        IList<string> urlsToCapture;
        int counter = 0;
        string currentURL;

        private void NavigateToNextUrl(object sender, RoutedEventArgs e)
        {
            if (counter < urlsToCapture.Count)
            {
                counter++;
                DisplayCounterUrl();
            }
            else
            {
                Application.Current.Exit();
            }
        }

        private void NavigateToPreviousUrl(object sender, RoutedEventArgs e)
        {
            if (counter > 1)
            {
                counter--;
                DisplayCounterUrl();
            }
        }

        private void counterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var candidateCounter = Int32.Parse(counterBox.Text);
            if (candidateCounter > 0 && candidateCounter <= urlsToCapture.Count)
            {
                counter = candidateCounter;
                DisplayCounterUrl();
            }
        }

        private void DisplayCounterUrl()
        {
            if (counter > 0 && counter <= urlsToCapture.Count)
            {
                currentURL = urlsToCapture[counter - 1];
                counterBox.Text = counter.ToString();
                counterView.Text = "/" + urlsToCapture.Count;
                urlView.Text = currentURL;
                RefreshCurrentUrl(null, null);
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

            try
            {
                // Capture a description of all chars/words/lines/blocks bounding boxes
                // Draw all these bounding boxes on the screen
                var pageElementsTree = await ScreenCapture.CreateAndSaveTextBoundingBoxes(webview, fileName);

                // Capture a new screenshot
                await ScreenCapture.CreateAndSaveScreenshotAsync(webview, captureBoxes, fileName, "boxes");
            }
            catch (Exception e)
            {
                var md = new MessageDialog(e.Message);
                await md.ShowAsync();
            }

            // Reset view to its original size
            ScreenCapture.SetViewDimensions(webview, viewDimensions);           
        }
    }
}
