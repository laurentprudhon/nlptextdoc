using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace nlptextdoc.image2
{
    class FilesManager
    {
        static string OUTPUT_DIR = "nlptextdoc.image2";

        static StreamReader OpenReaderForEmbeddedFile(string relativeFilePath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "nlptextdoc.image2." + relativeFilePath.Replace('/', '.');
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
                string line = null;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        internal static DirectoryInfo GetOutputFolder()
        {
            var picturesFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            var outputFolder = Directory.CreateDirectory(Path.Combine(picturesFolder, OUTPUT_DIR));
            return outputFolder;
        }

        internal static string GetOutputFolderPath()
        {
            return GetOutputFolder().FullName;
        }

        internal static string GetLastFileName() 
        {
            var outputFolder = GetOutputFolder();
            var lastFile = outputFolder.GetFiles("*.json").OrderByDescending(fi => fi.Name).FirstOrDefault();
            if (lastFile == null) { return null;  } else { return lastFile.Name; }
        }

        internal static int GetLastSavedCounter()
        {
            var lastFileName = Task.Run(GetLastFileName).Result;
            if(lastFileName == null) { return 0; }
            else { return Int32.Parse(lastFileName.Substring(0, 5)); }
        }

        internal static void WriteTextToFile(string fileName, string text)
        {
            var outputFolder = GetOutputFolderPath();
            var textFile = Path.Combine(outputFolder, fileName);
            File.WriteAllText(textFile, text);
        }

        internal static (string,Stream) GetStreamToWriteImage(string fileName)
        {
            var outputFolder = GetOutputFolderPath();
            var imageFile = Path.Combine(outputFolder, fileName);
            return (imageFile, new FileStream(imageFile, FileMode.OpenOrCreate, FileAccess.Write));
        }
    }
}
