using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web;

namespace nlptextdoc.image
{
    public sealed partial class MainPage : Page
    {
        // -- Configuration --
        const string DATASET = "Banque";
        const int STARTCOUNTER = 1;
        const bool INTERACTIVE = false;

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
                counter = FilesManager.GetLastSavedCounter();
            }
            NavigateToNextUrl(this, null);
        }

        // Internal state
        IList<string> urlsToCapture;
        int counter = 0;
        string currentURL;
        WebErrorStatus currentErrorStatus;

        private async void NavigateToNextUrl(object sender, RoutedEventArgs e)
        {
            await DoNavigateToNextUrl();
        }

        private async Task DoNavigateToNextUrl()
        {
            if (counter < urlsToCapture.Count)
            {
                counter++;
                await DisplayCounterUrl();
            }
            else
            {
                Application.Current.Exit();
            }
        }

        private async void NavigateToPreviousUrl(object sender, RoutedEventArgs e)
        {
            if (counter > 1)
            {
                counter--;
                await DisplayCounterUrl();
            }
        }

        private async void counterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var candidateCounter = Int32.Parse(counterBox.Text);
            if (candidateCounter > 0 && candidateCounter <= urlsToCapture.Count)
            {
                counter = candidateCounter;
                await DisplayCounterUrl();
            }
        }

        private async Task DisplayCounterUrl()
        {
            if (counter > 0 && counter <= urlsToCapture.Count)
            {
                currentURL = urlsToCapture[counter - 1];
                counterBox.Text = counter.ToString();
                counterView.Text = "/" + urlsToCapture.Count;
                urlView.Text = currentURL;
                currentErrorStatus = await RefreshCurrentUrl();
            }
        }

        private async void RefreshWebView(object sender, RoutedEventArgs e)
        {
            currentErrorStatus = await RefreshCurrentUrl();
        }

        private async Task<WebErrorStatus> RefreshCurrentUrl()
        {
            WebErrorStatus errorStatus = WebErrorStatus.Unknown;
            using (SemaphoreSlim semaphoreSlim = new SemaphoreSlim(0, 1))
            {  
                void handler(WebView s, WebViewNavigationCompletedEventArgs wvnce)
                {
                    errorStatus = wvnce.WebErrorStatus;
                    webview.NavigationCompleted -= handler;                    
                    semaphoreSlim.Release();
                }
                webview.NavigationCompleted += handler;
                webview.Navigate(new Uri(currentURL));
                await semaphoreSlim.WaitAsync().ConfigureAwait(false);
            }
            return errorStatus;
        }

        private async void CaptureScreenshots(object sender, RoutedEventArgs e)
        {
            await DoCaptureScreenshots();
        }

        private async void CaptureScreenshotsAndNavigateToNextURL(object sender, RoutedEventArgs e)
        {
            do
            {
                await DoCaptureScreenshots();
                await DoNavigateToNextUrl();
            } 
            while (!INTERACTIVE);
        }

        private async Task DoCaptureScreenshots()
        {
            // Get unique file name for the current URL
            var fileName = await JavascriptInterop.GetUniqueFileNameFromURLAsync(webview);
            fileName = counter.ToString("D5") + "_" + fileName;

            if (currentErrorStatus != WebErrorStatus.Unknown)
            {
                var errorString = Enum.GetName(typeof(WebErrorStatus), currentErrorStatus);
                var errorMessage = "Error navigating to " + currentURL + " => " + errorString;
                if (INTERACTIVE)
                {
                    var md = new MessageDialog(errorMessage);
                    await md.ShowAsync();
                }
                else
                {
                    FilesManager.WriteTextToFileAsync(fileName + "_error.log", errorMessage);
                }
            }
            else 
            { 
                // Get view and content dimensions
                var viewDimensions = ScreenCapture.GetViewDimensions(webview);
                var contentDimensions = await ScreenCapture.GetContentDimensionsAsync(webview);

                // Resize view to content size
                ScreenCapture.SetViewDimensions(webview, contentDimensions);

                // Capture a screenshot
                await ScreenCapture.CreateAndSaveScreenshotAsync(webview, capture, null, warmup: true); // necessary because sometimes screenshot alters layout
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
                    if (INTERACTIVE)
                    {
                        var md = new MessageDialog(e.Message);
                        await md.ShowAsync();
                    }
                    else
                    {
                        FilesManager.WriteTextToFileAsync(fileName + "_error.log", e.Message);
                    }
                }

                if (INTERACTIVE)
                {
                    // Reset view to its original size
                    ScreenCapture.SetViewDimensions(webview, viewDimensions);
                }
                else
                {
                    // Choose random width for next image
                    viewDimensions.width = ScreenCapture.GetRandowWidth();
                    ScreenCapture.SetViewDimensions(webview, viewDimensions);
                }
            }
        }
    }
}
