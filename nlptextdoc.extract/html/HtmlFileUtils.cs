using System;
using System.IO;
using System.Text;

namespace nlptextdoc.extract.html
{
    public static class HtmlFileUtils
    {
        public static FileInfo GetFilePathFromUri(DirectoryInfo contentDirectory, Uri uri)
        {
            StringBuilder filePath = new StringBuilder();
            filePath.Append(contentDirectory.FullName);
            filePath.Append(Path.DirectorySeparatorChar);

            foreach (var uriSegment in uri.Segments)
            {
                if (uriSegment == "/") continue;
                if (!uriSegment.EndsWith("/")) continue;
                filePath.Append(GetPathValidChars(uriSegment));
            }

            string fileName = Path.GetFileName(uri.AbsolutePath);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = "_default_";
            }
            if (!String.IsNullOrEmpty(uri.Query))
            {
                string hash;
                using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
                {
                    hash = BitConverter.ToString(
                      md5.ComputeHash(Encoding.UTF8.GetBytes(uri.Query))
                    ).Replace("-", String.Empty);
                }
                fileName += "_" + hash;
            }
            filePath.Append(GetFileNameValidChars(fileName));
            filePath.Append(".nlp.txt");

            return new FileInfo(filePath.ToString());
        }

        public static string GetPathValidChars(string pathSegment)
        {
            foreach (var item in Path.GetInvalidPathChars())
            {
                pathSegment = pathSegment.Replace(item.ToString(), "_");
            }
            return pathSegment;
        }

        private static string GetFileNameValidChars(string fileName)
        {
            foreach (var item in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(item.ToString(), "_");
            }
            return fileName;
        }
    }
}
