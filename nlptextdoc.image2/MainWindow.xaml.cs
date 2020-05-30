using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;

namespace nlptextdoc.image2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // -- Configuration --
        const string DATASET = "Assurance";
        const int STARTCOUNTER = 1;
        const bool INTERACTIVE = false;

        public MainWindow()
        {
            InitializeComponent();

            // Output folder link => clipboard
            var outputPath = FilesManager.GetOutputFolderPath();
            Clipboard.SetText(outputPath);

            // Initialize URLs list and navigate to next URL
            urlsToCapture = URLsSource.ReadDatasetURLs(DATASET).ToList();
            counter = STARTCOUNTER - 1;
            if (STARTCOUNTER == 1)
            {
                counter = FilesManager.GetLastSavedCounter();
            }
            InitializeWebViewAsync();
        }

        private async void InitializeWebViewAsync()
        {
            await webview.EnsureCoreWebView2Async(null);
            await DoNavigateToNextUrl();
        }
         
        // Internal state
        IList<string> urlsToCapture;
        int counter = 0;
        string currentURL;
        HttpStatusCode currentStatusCode;
        CoreWebView2WebErrorStatus currentErrorStatus;

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
                Application.Current.Shutdown();
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
            if (candidateCounter != counter)
            {
                if (candidateCounter > 0 && candidateCounter <= urlsToCapture.Count)
                {
                    counter = candidateCounter;
                    await DisplayCounterUrl();
                }
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
                (currentStatusCode, currentErrorStatus) = await RefreshCurrentUrl();
            }
        }

        private async void RefreshWebView(object sender, RoutedEventArgs e)
        {
            (currentStatusCode, currentErrorStatus) = await RefreshCurrentUrl();
        }

        private async Task<(HttpStatusCode,CoreWebView2WebErrorStatus)> RefreshCurrentUrl()
        {
            HttpClient client = new HttpClient();
            var response = await client.GetAsync(currentURL, HttpCompletionOption.ResponseHeadersRead);

            CoreWebView2WebErrorStatus errorStatus = CoreWebView2WebErrorStatus.Unknown;
            using (SemaphoreSlim semaphoreSlim = new SemaphoreSlim(0, 1))
            {
                void navigationCompletedHandler(object s, CoreWebView2NavigationCompletedEventArgs wvnce)
                {
                    ((CoreWebView2)s).NavigationCompleted -= navigationCompletedHandler;
                    errorStatus = wvnce.WebErrorStatus;
                    semaphoreSlim.Release();
                }
                webview.CoreWebView2.NavigationCompleted += navigationCompletedHandler;
                webview.CoreWebView2.Navigate(currentURL);
                await semaphoreSlim.WaitAsync().ConfigureAwait(false);
            }
            return (response.StatusCode,errorStatus);
        }

        private async void CaptureScreenshots(object sender, RoutedEventArgs e)
        {
            await DoCaptureScreenshots();
        }

        private async void CaptureScreenshotsAndNavigateToNextURL(object sender, RoutedEventArgs e)
        {
            do
            {
                try
                {
                    await DoCaptureScreenshots();
                    await DoNavigateToNextUrl();
                }
                catch (Exception ex)
                {
                    if (INTERACTIVE)
                    {
                        MessageBox.Show(ex.Message);
                    }
                    else
                    {
                        FilesManager.WriteTextToFile(counter.ToString("D5") + "_error.log", ex.Message);
                    }
                }
            }
            while (!INTERACTIVE);
        }

        private async Task DoCaptureScreenshots()
        {
            // Get unique file name for the current URL
            var fileName = await JavascriptInterop.GetUniqueFileNameFromURLAsync(webview.CoreWebView2);
            fileName = counter.ToString("D5") + "_" + fileName;

            if ((int)currentStatusCode >= 400 ||  currentErrorStatus != CoreWebView2WebErrorStatus.Unknown)
            {
                var errorString = Enum.GetName(typeof(CoreWebView2WebErrorStatus), currentErrorStatus);
                var errorMessage = "Error navigating to " + currentURL + " => " + currentStatusCode + " / " + errorString;
                if (INTERACTIVE)
                {
                    MessageBox.Show(errorMessage);
                }
                else
                {
                    FilesManager.WriteTextToFile(fileName + "_error.log", errorMessage);
                }
            }
            else
            {
                // Get view and content dimensions
                var viewDimensions = ScreenCapture.GetViewDimensions(webview);
                var contentDimensions = await ScreenCapture.GetContentDimensionsAsync(webview.CoreWebView2);

                // Resize view to content size
                ScreenCapture.SetViewDimensions(webview, contentDimensions);

                // Wait 3 seconds for display to adjust
                Thread.Sleep(3000);

                try
                {
                    PageElement pageElementsTree = null;
                    int screenshotHeight = 0;
                    int retryCount = 0;
                    do
                    {
                        retryCount++;

                        // Capture a description of all chars/words/lines/blocks bounding boxes                    
                        pageElementsTree = await ScreenCapture.CreateAndSaveTextBoundingBoxes(webview.CoreWebView2, fileName);

                        // Capture a screenshot
                        var screenFile = await ScreenCapture.CreateAndSaveScreenshotAsync(webview.CoreWebView2, fileName);

                        // Draw the bounding boxes on a second screenshot
                        var boxesFile = MaskGenerator.DrawBoundingBoxes(screenFile, pageElementsTree);

                        // Display both screenshots on screen
                        screenshotHeight = SixLabors.ImageSharp.Image.Identify(screenFile).Height;
                        DisplayScreenshot(screenFile, captureScreen);
                        DisplayScreenshot(boxesFile, captureBoxes);                       
                    }
                    // Check consistency
                    while (retryCount <= 3 && screenshotHeight != pageElementsTree.boundingBox.height);                    
                }
                catch (Exception e)
                {
                    if (INTERACTIVE)
                    {
                        MessageBox.Show(e.Message);
                    }
                    else
                    {
                        var message = e.Message;
                        message += "\n" + e.StackTrace;
                        if(e.InnerException != null)
                        {
                            message += "\n" + e.InnerException.Message;
                            message += "\n" + e.InnerException.StackTrace;
                        }
                        FilesManager.WriteTextToFile(fileName + "_error.log", message);
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

        private static void DisplayScreenshot(string filePath, Image target)
        {
            // Display image from disk
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(filePath);
            image.EndInit();
            target.Source = image;
        }
    }
}
