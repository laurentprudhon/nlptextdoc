using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace nlptextdoc.extract.html
{
    public enum ExtractionScope
    {
        Domain,
        SubDomain,
        Path
    }

    /// <summary>
    /// Parameters received from the command line to configure the website extraction
    /// </summary>
    public class WebsiteExtractorParams
    {
        public WebsiteExtractorParams()
        {
            // Default values for command line parameters
            Scope = ExtractionScope.Domain;
            MaxDuration = 2;
            MaxPageCount = 500;
            MinUniqueText = 10;
            MaxSizeOnDisk = 0;
            MinCrawlDelay = 100;
        }

        /// <summary>
        /// domain | subdomain | path
        ///  > decide what part of the rootUrl should be used to limit the extraction
        /// </summary>
        public ExtractionScope Scope;

        /// <summary>
        /// Root Url of the website (or subfolder of a website) you want to crawl
        /// </summary>
        public string RootUrl;

        /// <summary>
        /// Path to the disk directory where the text documents will be extracted
        /// </summary>
        public string StorageDir;

        /// <summary>
        /// Maximum duration of the extraction in minutes
        /// </summary>
        public int MaxDuration;
        /// <summary>
        /// Maximum number of pages extracted from the website
        /// </summary>
        public int MaxPageCount;
        /// <summary>
        /// Minimum percentage of unique text blocks extracted"
        /// </summary>
        public int MinUniqueText;
        /// <summary>
        /// Maximum size of the extracted text files on disk in Mb
        /// </summary>
        public int MaxSizeOnDisk;

        /// <summary>
        /// Delay in milliseconds between two requests sent to the website
        /// </summary>
        public int MinCrawlDelay;

        public void WriteToFile(StreamWriter sw)
        {
            sw.WriteLine("# --- nlptextdoc config file ---");
            sw.WriteLine("# First launch : " + DateTime.Now.ToString());
            sw.WriteLine();
            sw.WriteLine("# Decide what part of the rootUrl should be used to limit the extraction");
            sw.WriteLine("# (domain | subdomain | path)");
            sw.WriteLine("scope=" + Scope.ToString().ToLower());
            sw.WriteLine("# Root Url of the website (or subfolder of a website) you want to crawl");
            sw.WriteLine("rootUrl=" + RootUrl);
            sw.WriteLine("# Path to the disk directory where the text documents will be extracted");
            sw.WriteLine("storageDir=" + StorageDir);
            sw.WriteLine();
            sw.WriteLine("# Stopping conditions");
            sw.WriteLine("# Maximum duration of the extraction in minutes");
            sw.WriteLine("maxDuration=" + MaxDuration);
            sw.WriteLine("# Maximum number of pages extracted from the website"); 
            sw.WriteLine("maxPageCount=" + MaxPageCount);
            sw.WriteLine("# Minimum percentage of unique text blocks extracted");
            sw.WriteLine("minUniqueText=" + MinUniqueText);
            sw.WriteLine("# Maximum size of the extracted text files on disk in Mb");
            sw.WriteLine("maxSizeOnDisk=" + MaxSizeOnDisk);
            sw.WriteLine();
            sw.WriteLine("# Throttling");
            sw.WriteLine("# Delay in milliseconds between two requests sent to the website");
            sw.WriteLine("minCrawlDelay=" + MinCrawlDelay);
        }

        public static WebsiteExtractorParams ReadFromFile(StreamReader sr)
        {
            var extractorParams = new WebsiteExtractorParams();

            string line = null;
            while((line = sr.ReadLine()) != null)
            {
                if(!String.IsNullOrEmpty(line) && !line.StartsWith("#"))
                {
                    extractorParams.ParseParam(line);
                }
            }

            return extractorParams;
        }

        public void ParseParam(string keyValueParam)
        {
            int equalsIndex = keyValueParam.IndexOf("=");
            if (equalsIndex < 0 || equalsIndex == (keyValueParam.Length - 1))
            {
                throw new Exception("Syntax error in params at : " + keyValueParam);
            }
            string[] keyValue = keyValueParam.Split('=');
            string key = keyValue[0].Trim().ToLower();
            string value = keyValue[1].Trim();
            switch (key)
            {
                case "scope":
                    switch (value.ToLower())
                    {
                        case "domain":
                            Scope = ExtractionScope.Domain;
                            break;
                        case "subdomain":
                            Scope = ExtractionScope.SubDomain;
                            break;
                        case "path":
                            Scope = ExtractionScope.Path;
                            break;
                        default:
                            throw new Exception("Invalid value for key scope at : " + keyValueParam);
                    }
                    break;
                case "rooturl":
                    RootUrl = value;
                    break;
                case "storagedir":
                    StorageDir = value;
                    break;
                case "maxduration":
                    MaxDuration = Int32.Parse(value);
                    break;
                case "maxpagecount":
                    MaxPageCount = Int32.Parse(value);
                    break;
                case "minuniquetext":
                    MinUniqueText = Int32.Parse(value);
                    break;
                case "maxsizeondisk":
                    MaxSizeOnDisk = Int32.Parse(value);
                    break;
                case "mincrawldelay":
                    MinCrawlDelay = Int32.Parse(value);
                    break;
                default:
                    throw new Exception("Invalid parameter at : " + keyValueParam);
            }
        }
    }
}
