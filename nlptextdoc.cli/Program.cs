using nlptextdoc.extract.html;
using System;

namespace nlptextdoc.cli
{
    class Program
    {
        static string version = "1.2";

        static void PrintUsage()
        {
            Console.WriteLine("nlptexdoc extractor v" + version);
            Console.WriteLine();
            Console.WriteLine("Crawls all the Html pages of a website and converts them to .nlp.txt structured text documents.");
            Console.WriteLine("All the extracted text documents are stored under a single directory named like the website.");
            Console.WriteLine("The .nlp.txt file format is described here : https://www.cognitivefactory.org/nlptextdocument/");
            Console.WriteLine();
            Console.WriteLine("Features an advanced Html to text conversion algorithm :");
            Console.WriteLine("- tries to recover the logical structure of the document from the Html layout");
            Console.WriteLine("- interprets Css properties of the Html nodes to make this operation more reliable");
            Console.WriteLine("- preserves document / section / list / table grouping and nesting information");
            Console.WriteLine();
            Console.WriteLine("Usage to launch a website extraction:");
            Console.WriteLine();
            Console.WriteLine("nlptextdoc [scope] [rootUrl] [key=value optional params]");
            Console.WriteLine(" - scope            : domain | subdomain | path");
            Console.WriteLine("                      > decide what part of the rootUrl should be used to limit the extraction");
            Console.WriteLine(" - rootUrl          : root Url of the website (or subfolder of a website) you want to crawl");
            Console.WriteLine();
            Console.WriteLine("Usage to continue or restart after a first try:");
            Console.WriteLine();
            Console.WriteLine("nlptextdoc [continue|restart] [rootUrl] [key=value optional params to override]");
            Console.WriteLine();
            Console.WriteLine("Optional parameters :");
            Console.WriteLine(" - minCrawlDelay=100 : delay in milliseconds between two requests sent to the website");
            Console.WriteLine(" - excludeUrls=/*.php$ : stop extracting text from urls starting with this pattern");
            Console.WriteLine();
            Console.WriteLine("Optional stopping conditions (the first to be met will stop the crawl, 0 means no limit) :");
            Console.WriteLine(" - maxDuration=0     : maximum duration of the extraction in minutes");
            Console.WriteLine(" - maxPageCount=0  : maximum number of pages extracted from the website");
            Console.WriteLine(" - maxErrorsCount=10  : maximum number of errors during the extraction");
            Console.WriteLine(" - minUniqueText=10  : minimum percentage of unique text blocks extracted");
            Console.WriteLine(" - maxSizeOnDisk=0   : maximum size of the extracted text files on disk in Mb");
            Console.WriteLine();
            Console.WriteLine("Recommended process :");
            Console.WriteLine("0. Navigate to the rootUrl in your browser and check the links on the page to select a scope for the extraction");
            Console.WriteLine("1. Run the the tool with the default params (crawl delay = 100 ms) until the extraction is stopped after 10 errors");
            Console.WriteLine("2. Open the log file \"_nlptextdoc/httprequests.log.csv\" created in the storageDirectory for the website");
            Console.WriteLine("3. Check for Http \"Forbidden\" answers or connection errors, and test if the url is accessible from your browser");
            Console.WriteLine("4. Restart the extraction with a bigger minCrawlDelay, and continue to increase it until \"Forbidden\" errors disappear");
            Console.WriteLine("5. Restart the extraction with an additional urlPatternsToExclude until you get more unique text blocks");
            Console.WriteLine();
            Console.WriteLine("The extraction can take a while :");
            Console.WriteLine("- your system can go to hibernation mode and resume without interrupting the crawl");
            Console.WriteLine("- your can even stop the crawl (Ctrl-C or shutdown) and continue it later where you left it");
            Console.WriteLine("- the continue command will use checkpoint and config files found in the \"_nlptextdoc\" subfolder");
            Console.WriteLine("- the restart command will ignore any checkpoint, start again at the root url, and overwrite everything");
            Console.WriteLine();
        }

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                PrintUsage();
            }
            else
            {
                var command = args[0].Trim().ToLower();
                var rootUrl = args[1].Trim();
                var storageDir = "."; // Always extract data in the current directory

                bool doRestart = false;
                bool doContinue = false;
                if(command == "restart")
                {
                    doRestart = true;
                }
                else if(command == "continue")
                {
                    doContinue = true;
                }
                if(doRestart || doContinue)
                {
                    // Ability to override the previous parameters with new values
                    string[] newParams = null;
                    if (args.Length > 2)
                    {
                        newParams = new string[args.Length - 2];
                        for(int i = 2; i<args.Length; i++)
                        {
                            newParams[i-2] = args[i];
                        }
                    }
                    using (var websiteTextExtractor = new WebsiteTextExtractor(storageDir, rootUrl, newParams, doContinue))
                    {
                        websiteTextExtractor.ExtractNLPTextDocuments();
                    }
                }
                else
                {
                    WebsiteExtractorParams extractorParams = new WebsiteExtractorParams();
                    extractorParams.ParseParam("scope=" + command);                        
                    extractorParams.ParseParam("rootUrl=" + rootUrl);
                    extractorParams.ParseParam("storageDir=" + storageDir);

                    for(int i = 2; i<args.Length; i++)
                    {
                        var keyValueParam = args[i];
                        extractorParams.ParseParam(keyValueParam);
                    }

                    using (var websiteTextExtractor = new WebsiteTextExtractor(extractorParams))
                    {
                        websiteTextExtractor.ExtractNLPTextDocuments();
                    }
                }
            }
        }
    }
}
