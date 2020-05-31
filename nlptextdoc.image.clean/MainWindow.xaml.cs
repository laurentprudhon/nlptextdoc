using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace nlptextdoc.image.clean
{    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Config
        private static int IMAGES_DIRECTORY_INDEX = 0;

        public MainWindow()
        {
            InitializeComponent();

            // Images folder and errors subdirectory
            var outputFolder = GetOutputFolder().GetDirectories()[IMAGES_DIRECTORY_INDEX];
            errorFolder = outputFolder.CreateSubdirectory("errors");

            // Group files in images folder by number
            var fileGroups = outputFolder.GetFiles().OrderBy(fi => fi.Name).GroupBy(fi => fi.Name.Substring(0,5));
            fileGroupsEnumerator = fileGroups.GetEnumerator();
            totalView.Text = " / " + fileGroups.Last().Key;
            DisplayNextFileGroup(null,null);
        }

        private void DisplayNextFileGroup(object sender, RoutedEventArgs args)
        {
            if (fileGroupsEnumerator.MoveNext())
            {
                currentFileGroup = fileGroupsEnumerator.Current;
                counterView.Text = currentFileGroup.Key;

                if (currentFileGroup.Count() < 3)
                {
                    DeleteFiles(null, null);
                }
                else
                {
                    var jsonFile = currentFileGroup.Where(fi => fi.Name.EndsWith(".json")).FirstOrDefault();
                    var screenFile = currentFileGroup.Where(fi => fi.Name.EndsWith("_screen.png")).FirstOrDefault();
                    var boxesFile = currentFileGroup.Where(fi => fi.Name.EndsWith("_boxes.png")).FirstOrDefault();
                    if (jsonFile == null || screenFile == null || boxesFile == null ||
                        jsonFile.Length == 0 || screenFile.Length == 0 || boxesFile.Length == 0 ||
                        screenFile.Name.Contains("chromewebdata"))
                    {
                        DeleteFiles(null, null);
                    }
                    else
                    {
                        fileView.Text = jsonFile.Name + " (" + jsonFile.Length + ")";
                        var screenDims = DisplayImage(captureScreen, screenFile.FullName);
                        var boxesDims = DisplayImage(captureBoxes, boxesFile.FullName);
                        if (screenDims != boxesDims)
                        {
                            DeleteFiles(null, null);
                        }
                        scroll2.ScrollToEnd();
                    }
                }
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        private (int,int) DisplayImage(Image screenshot, string filePath)
        {
            // Display image from disk
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(filePath);
            image.EndInit();
            screenshot.Source = image;
            return (image.PixelWidth,image.PixelHeight);
        }

        private DirectoryInfo errorFolder;

        private void DeleteFiles(object sender, RoutedEventArgs args)
        {
            foreach (var file in currentFileGroup)
            {
                file.MoveTo(Path.Combine(errorFolder.FullName, file.Name));
            }
            DisplayNextFileGroup(null, null);
        }

        private IEnumerator<IGrouping<string, FileInfo>> fileGroupsEnumerator;
        private IGrouping<string, FileInfo> currentFileGroup;

        static string OUTPUT_DIR = "nlptextdoc.image2";

        internal static DirectoryInfo GetOutputFolder()
        {
            var picturesFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            var outputFolder = Directory.CreateDirectory(Path.Combine(picturesFolder, OUTPUT_DIR));
            return outputFolder;
        }

        private void scroll1_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (scroll1.VerticalOffset != scroll2.VerticalOffset)
            {
                scroll2.ScrollToVerticalOffset(scroll1.VerticalOffset);
            }
        }

        private void scroll2_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (scroll2.VerticalOffset != scroll1.VerticalOffset)
            {
                scroll1.ScrollToVerticalOffset(scroll2.VerticalOffset);
            }
        }
    }
}
