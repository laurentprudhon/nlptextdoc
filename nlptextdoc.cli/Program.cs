using nlptextdoc.extract.html;
using System;

namespace nlptextdoc.cli
{
    class Program
    {
        static string version = "1.0";

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
            Console.WriteLine("Usage : nlptextdoc [scope] [rootUrl] [storageDir] [key=value optional params]");
            Console.WriteLine(" - scope            : domain | subdomain | path");
            Console.WriteLine("                      > decide what part of the rootUrl should be used to limit the extraction");
            Console.WriteLine(" - rootUrl          : root Url of the website (or subfolder of a website) you want to crawl");
            Console.WriteLine(" - storageDir       : path to the disk directory where the text documents will be extracted");
            Console.WriteLine("Optional stopping conditions (the first to be met will stop the crawl, 0 means no limit) :");
            Console.WriteLine(" - maxDuration=2     : maximum duration of the extraction in minutes");
            Console.WriteLine(" - maxPageCount=500  : maximum number of pages extracted from the website");
            Console.WriteLine(" - minUniqueText=10  : minimum percentage of unique text blocks extracted");
            Console.WriteLine(" - maxSizeOnDisk=0   : maximum size of the extracted text files on disk in Mb");
            Console.WriteLine("Optional parameters :");
            Console.WriteLine(" - minCrawlDelay=100 : delay in milliseconds between two requests sent to the website");
            Console.WriteLine();
            Console.WriteLine("Recommended process :");
            Console.WriteLine("0. Navigate to the rootUrl in your browser and check the links on the page to select a scope for the extraction");
            Console.WriteLine("1. Run the the tool once with the default params (maximum 2 minutes/500 pages, small crawl delay)");
            Console.WriteLine("2. Open the log file \"_nlptextdoc/httprequests.log.csv\" created in the storageDirectory for the website");
            Console.WriteLine("3. Check for Http \"Forbidden\" answers, and test if the url was accessible when tested from your browser");
            Console.WriteLine("4. Try again with a bigger minCrawlDelay, and continue to increase it until \"Forbidden\" errors disappear");
            Console.WriteLine("5. Open the log file \"_nlptextdoc/exceptions.log.txt\" created in the storageDirectory for the website");
            Console.WriteLine("6. Try to find the root cause and to fix any exception message you see there");
            Console.WriteLine("7. Start the extraction again with bigger maxPageCount and maxDuration");
            Console.WriteLine();
            Console.WriteLine("The extraction can take a while :");
            Console.WriteLine("- your system can go to hibernation mode and resume without interrupting the crawl");
            Console.WriteLine("- your can even stop the crawl (Ctrl-C or shutdown) and continue it later where you left it");
            Console.WriteLine("- the continue command will use checkpoints and params traces found in the \"_nlptextdoc\" subfolder");
            Console.WriteLine("- the restart command will ignore checkpoints, start again at the root url, and overwrite everything");
            Console.WriteLine();
            Console.WriteLine("Specific syntax to continue or restart after a first try :");
            Console.WriteLine("nlptextdoc [continue|restart] [storageDirectory/rootUrlSubdir] [key=value optional params to override]");
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
                    var storageDirForWebsite = args[1].Trim();
                    // Ability to override the previous parameters with new values
                    string[] newParams = null;
                    if (args.Length > 2)
                    {
                        newParams = new string[args.Length - 2];
                        for(int i = 0; i<(args.Length - 2); i++)
                        {
                            newParams[i] = args[i + 2];
                        }
                    }
                    using (var websiteTextExtractor = new WebsiteTextExtractor(storageDirForWebsite, newParams, doContinue))
                    {
                        websiteTextExtractor.ExtractNLPTextDocuments();
                    }
                }
                else
                {
                    if (args.Length < 3)
                    {
                        PrintUsage();
                    }
                    else
                    {
                        WebsiteExtractorParams extractorParams = new WebsiteExtractorParams();
                        extractorParams.ParseParam("scope=" + command);
                        var rootUrl = args[1].Trim();
                        extractorParams.ParseParam("rootUrl=" + rootUrl);
                        var storageDir = args[2].Trim();
                        extractorParams.ParseParam("storageDir=" + storageDir);

                        for(int i = 3; i<args.Length; i++)
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
}
