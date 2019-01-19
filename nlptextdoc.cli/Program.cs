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
            Console.WriteLine("Usage : nlptextdoc [rootUrl] [storageDirectory] [maxPagesCount=0] [minCrawlDelay=0]");
            Console.WriteLine(" - rootUrl          : root Url of the website (or subfolder of a website) you want to crawl");
            Console.WriteLine(" - storageDirectory : path to the disk directory where the website folder");
            Console.WriteLine(" - maxPagesCount    : maximum number of pages extracted from the website");
            Console.WriteLine(" - minCrawlDelay    : delay in milliseconds between two requests sent to the website");
            Console.WriteLine();
            Console.WriteLine("Recommended process :");
            Console.WriteLine("1. Run the the tool for the first time with a low maxPagesCount (for example 100) and no crawl delay");
            Console.WriteLine("2. Open the log file \"httprequests.log.csv\" (created at the root of the website directory) in a spreadsheet");
            Console.WriteLine("3. Check for Http \"Forbidden\" answers, and test if the url is accessible when tested from a browser");
            Console.WriteLine("4. Try again with a non null minCrawlDelay, and continue to inscrease it until \"Forbidden\" errors disappear");
            Console.WriteLine("5. Run the the tool with an intermediate maxPagesCount (for example 500) and an adequate crawl delay");
            Console.WriteLine("6. Look at the extracted pages and search for Urls that you would like to exclude");
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
                var rootUri = args[0];

                var storagePath = args[1];

                int maxPagesCount = 0;
                if (args.Length > 2)
                {
                    maxPagesCount = Int32.Parse(args[2]);
                }

                int minCrawlDelay = 0;
                if (args.Length > 3)
                {
                    minCrawlDelay = Int32.Parse(args[3]);
                }

                using (var websiteTextExtractor = new WebsiteTextExtractor(rootUri, storagePath, maxPagesCount, minCrawlDelay))
                {
                    websiteTextExtractor.ExtractNLPTextDocuments();
                }
            }
        }
    }
}
