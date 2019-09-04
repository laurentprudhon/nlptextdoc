using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace nlptextdoc.extract.html
{
    public static class HtmlFileUtils
    {
        /// <summary>
        /// Method called to decide which pages to crawl
        /// </summary>
        public static bool ShouldCrawlUri(ExtractionScope scope, Uri candidateUri, Uri rootUri)
        {
            switch (scope)
            {
                case ExtractionScope.Domain:
                    return GetBaseDomain(candidateUri) == GetBaseDomain(rootUri);
                case ExtractionScope.SubDomain:
                    return GetSubDomain(candidateUri) == GetSubDomain(rootUri);
                //case ExtractionScope.Path:
                default:
                    if (GetSubDomain(candidateUri) == GetSubDomain(rootUri))
                    {
                        return candidateUri.AbsolutePath.StartsWith(GetRootPath(rootUri.AbsolutePath));
                    }
                    else
                    {
                        return false;
                    }
            }
        }

        /// <summary>
        /// Returns the base domain from a domain name
        /// Example: http://www.west-wind.com returns west-wind.com
        /// </summary>
        private static string GetBaseDomain(Uri uri)
        {
            if (uri.HostNameType == UriHostNameType.Dns)
            {
                var domainName = uri.DnsSafeHost;
                var tokens = domainName.Split('.');

                if (tokens == null || tokens.Length < 3)
                    return domainName;

                return tokens[tokens.Length - 2] + "." + tokens[tokens.Length - 1];
            }
            else
            {
                return uri.Host;
            }
        }

        private static string GetSubDomain(Uri uri)
        {
            if (uri.HostNameType == UriHostNameType.Dns)
            {
                return uri.DnsSafeHost;
            }
            else
            {
                return uri.Host;
            }
        }

        private static string GetRootPath(string absolutePath)
        {
            int dotIndex = absolutePath.IndexOf('.');
            if (dotIndex > 0)
            {
                int slashIndex = absolutePath.LastIndexOf('/', dotIndex);
                if (slashIndex >= 0)
                {
                    return absolutePath.Substring(0, slashIndex + 1);
                }
            }
            return absolutePath;
        }

        public static string GetWebsitePathFromUri(ExtractionScope scope, Uri rootUri)
        {
            string websitePath = null;
            switch (scope)
            {
                case ExtractionScope.Domain:
                    websitePath = HtmlFileUtils.GetPathValidChars(GetBaseDomain(rootUri));
                    break;
                case ExtractionScope.SubDomain:
                    websitePath = HtmlFileUtils.GetPathValidChars(GetSubDomain(rootUri));
                    break;
                //case ExtractionScope.Path:
                default:
                    websitePath = HtmlFileUtils.GetPathValidChars(GetSubDomain(rootUri) + GetRootPath(rootUri.AbsolutePath).Replace("/", "_"));
                    break;
            }
            return websitePath;
        }

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
            filePath.Append(GetPathValidChars(fileName));
            filePath.Append(".nlp.txt");

            return new FileInfo(filePath.ToString());
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

        public static string GetPathValidChars(string path)
        {
            path = WebUtility.UrlDecode(path);
            path = string.Join("_", path.Split(invalidChars));
            path = multipleDotsInPath.Replace(path,"/");
            return path;
        }
    }
}
