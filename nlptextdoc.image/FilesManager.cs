using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace nlptextdoc.image
{
    class FilesManager
    {
        static string OUTPUT_DIR = "nlptextdoc.image";

        static StreamReader OpenReaderForEmbeddedFile(string relativeFilePath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "nlptextdoc.image." + relativeFilePath.Replace('/', '.');
            var stream = assembly.GetManifestResourceStream(resourceName);
            return new StreamReader(stream);
        }

        internal static string ReadStringFromEmbeddedFile(string relativeFilePath)
        {
            using (var reader = OpenReaderForEmbeddedFile(relativeFilePath))
            {
                return reader.ReadToEnd();
            }
        }

        internal static IEnumerable<string> ReadLinesFromEmbeddedFile(string relativeFilePath)
        {
            using (var reader = OpenReaderForEmbeddedFile(relativeFilePath))
            {
                yield return reader.ReadLine();
            }
        }

        internal async static Task<StorageFolder> GetOutputFolder()
        {
            var picturesFolder = KnownFolders.SavedPictures;
            var outputFolder = await picturesFolder.CreateFolderAsync(OUTPUT_DIR, CreationCollisionOption.OpenIfExists);
            return outputFolder;
        }

        internal static async void WriteTextToFile(string fileName, string text)
        {
            var outputFolder = await GetOutputFolder();
            var textFile = await outputFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            using (var stream = await textFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                using (IOutputStream outputStream = stream.GetOutputStreamAt(0))
                {
                    using (DataWriter dataWriter = new DataWriter(outputStream))
                    {
                        dataWriter.WriteString(text);
                        await dataWriter.StoreAsync();
                        dataWriter.DetachStream();
                    }
                    await outputStream.FlushAsync();
                }
            }
        }

        internal static async void WriteImageToFile(string fileName, uint width, uint height, byte[] pixels)
        {
            var outputFolder = await GetOutputFolder();
            var imageFile = await outputFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            /*var propertySet = new Windows.Graphics.Imaging.BitmapPropertySet();
            var qualityValue = new Windows.Graphics.Imaging.BitmapTypedValue(1.0, Windows.Foundation.PropertyType.Single);
            propertySet.Add("ImageQuality", qualityValue);*/
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(
                BitmapEncoder.PngEncoderId, 
                await imageFile.OpenAsync(FileAccessMode.ReadWrite)/*, propertySet*/);
            
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                width,
                height,
                DisplayInformation.GetForCurrentView().LogicalDpi,
                DisplayInformation.GetForCurrentView().LogicalDpi,
                pixels);
            await encoder.FlushAsync();
        }
    }
}
