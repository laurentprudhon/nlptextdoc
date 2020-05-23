using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace nlptextdoc.image2
{
    static class HtmlFileUtils
    {        
        internal static string GetFileNameFromUri(string uriString, int maxLength = 100)
        {
            StringBuilder filePath = new StringBuilder();

            Uri uri = new Uri(uriString);

            filePath.Append(GetPathValidChars(uri.Host));
            filePath.Append("__");

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
            filePath.Append(GetPathValidChars(fileName));

            return filePath.ToString(0, filePath.Length>maxLength?maxLength:filePath.Length).Replace('/','_');
        }

        private static char[] invalidChars;
        static HtmlFileUtils()
        {
            char[] fileInvalidChars = Path.GetInvalidFileNameChars();
            int slashIndex = Array.IndexOf(fileInvalidChars, '/');
            if (slashIndex >= 0)
            {
                invalidChars = new char[fileInvalidChars.Length-1];
                for (int i = 0; i < slashIndex; i++)
                    invalidChars[i] = fileInvalidChars[i];
                if(slashIndex < (fileInvalidChars.Length - 1))
                {
                    for (int i = slashIndex+1; i < fileInvalidChars.Length; i++)
                        invalidChars[i] = fileInvalidChars[i+1];
                }
            }
            else
            {
                invalidChars = fileInvalidChars;
            }
        }

        private static Regex multipleDotsInPath = new Regex("\\.+/", RegexOptions.Compiled);
        private static string GetPathValidChars(string path)
        {
            path = WebUtility.UrlDecode(path);
            path = string.Join("_", path.Split(invalidChars));
            path = multipleDotsInPath.Replace(path,"/");
            return path;
        }
    }
}
