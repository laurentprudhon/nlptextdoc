using Abot.Core;
using Abot.Crawler;
using Abot.Poco;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Dom.Events;
using AngleSharp.Dom.Html;
using AngleSharp.Network;
using nlptextdoc.extract.pdf;
using nlptextdoc.text.document;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using UglyToad.PdfPig;

namespace nlptextdoc.extract.html
{
    public class WebsiteTextExtractor : IDisposable
    {        
        public WebsiteTextExtractor(WebsiteExtractorParams extractorParams)
        {
            // Save params
            ExtractorParams = extractorParams;
            
            Init();
        }        

        public WebsiteTextExtractor(string storageDirForWebsite, string[] newParams, bool doContinue = false)
        {
            // Save action requested
            DoContinue = doContinue;

            // Reload params file 
            FileInfo paramFileInfo = new FileInfo(Path.Combine(storageDirForWebsite, LogsDirName, ConfigFileName));
            if(!paramFileInfo.Exists)
            {
                throw new Exception("No configuration file found at : " + paramFileInfo.FullName);
            }
            using (StreamReader sr = new StreamReader(paramFileInfo.FullName))
            {
                ExtractorParams = WebsiteExtractorParams.ReadFromFile(sr);
            }
            // Override with new params
            if(newParams != null)
            {
                foreach(string keyValueParam in newParams)
                {
                    ExtractorParams.ParseParam(keyValueParam);
                }
            }

            Init();
        }

        private void Init()
        {
            // Initialize the content directory and log files
            ConfigureStorageDirectories();
            InitLogFiles();

            // Initialize the extraction task
            ConfigureWebCrawler();
            ConfigureHtmlParser();
        }

        // Action requested : start a new extraction or continue a previous extraction
        public bool DoContinue { get; private set; }

        // Store configuration params
        public WebsiteExtractorParams ExtractorParams { get; private set; }

        // Web crawler engine
        private PoliteWebCrawler crawler;
        private Scheduler scheduler;

        // Directory where the extracted text files will be stored
        public DirectoryInfo ContentDirectory { get; private set; }

        // Measuring perfs while crawling the website
        public PerfMonitor Perfs { get; private set; }

        private void ConfigureWebCrawler()
        {
            CrawlConfiguration config = new CrawlConfiguration();

            config.MaxConcurrentThreads = 4; // Environment.ProcessorCount;
            config.MaxPagesToCrawl = 0;
            config.MaxPagesToCrawlPerDomain = 0;
            config.MaxPageSizeInBytes = 0;
            config.UserAgentString = "Mozilla/5.0 (Windows NT 6.3; Trident/7.0; rv:11.0) like Gecko";
            config.HttpProtocolVersion = HttpProtocolVersion.NotSpecified;
            config.CrawlTimeoutSeconds = 0;
            config.IsUriRecrawlingEnabled = false;
            config.IsExternalPageCrawlingEnabled = false;
            config.IsExternalPageLinksCrawlingEnabled = false;
            config.IsRespectUrlNamedAnchorOrHashbangEnabled = false;
            config.DownloadableContentTypes = "text/html, text/plain, application/pdf";
            config.HttpServicePointConnectionLimit = 200;
            config.HttpRequestTimeoutInSeconds = 15;
            config.HttpRequestMaxAutoRedirects = 7;
            config.IsHttpRequestAutoRedirectsEnabled = true;
            config.IsHttpRequestAutomaticDecompressionEnabled = true;
            config.IsSendingCookiesEnabled = false;
            config.IsSslCertificateValidationEnabled = false;
            config.MinAvailableMemoryRequiredInMb = 0;
            config.MaxMemoryUsageInMb = 0;
            config.MaxMemoryUsageCacheTimeInSeconds = 0;
            config.MaxCrawlDepth = 1000;
            config.MaxLinksPerPage = 1000;
            config.IsForcedLinkParsingEnabled = false;
            config.MaxRetryCount = 0;
            config.MinRetryDelayInMilliseconds = 0;

            config.IsRespectRobotsDotTextEnabled = true;
            config.UrlPatternsToExclude = ExtractorParams.UrlPatternsToExclude;
            config.IsRespectMetaRobotsNoFollowEnabled = true;
            config.IsRespectHttpXRobotsTagHeaderNoFollowEnabled = true;
            config.IsRespectAnchorRelNoFollowEnabled = true;
            config.IsIgnoreRobotsDotTextIfRootDisallowedEnabled = false;
            config.RobotsDotTextUserAgentString = "bingbot";
            config.MinCrawlDelayPerDomainMilliSeconds = ExtractorParams.MinCrawlDelay;
            config.MaxRobotsDotTextCrawlDelayInSeconds = 5;

            config.IsAlwaysLogin = false;
            config.LoginUser = "";
            config.LoginPassword = "";
            config.UseDefaultCredentials = false;

            if (!DoContinue)
            {
                scheduler = new Scheduler(config.IsUriRecrawlingEnabled, null, null);
            }
            else
            {
                using (FileStream fs = new FileStream(Path.Combine(ContentDirectory.FullName, LogsDirName, CheckpointFileName), FileMode.Open))
                {
                    scheduler = Scheduler.Deserialize(fs);
                }
            }
            crawler = new PoliteWebCrawler(config, null, null, scheduler, null, null, null, null, null);
            crawler.IsInternalUri((candidateUri,rootUri) => HtmlFileUtils.ShouldCrawlUri(ExtractorParams.Scope, candidateUri, rootUri));
            crawler.ShouldCrawlPageLinks(WebCrawler_ShouldCrawlPageLinks);
            crawler.PageCrawlCompletedAsync += WebCrawler_PageCrawlCompletedAsync;

            // DEBUG: uncomment to debug Abot crawl progress
            // crawler.PageCrawlStartingAsync += WebCrawler_PageCrawlStartingAsync;

            // DEBUG: uncomment to debug Abot crawling decisions
            // crawler.PageCrawlDisallowedAsync += WebCrawler_PageCrawlDisallowedAsync;
            // crawler.PageLinksCrawlDisallowedAsync += WebCrawler_PageLinksCrawlDisallowedAsync;
        }

        // Html parser browsing context
        IBrowsingContext context;

        private void ConfigureHtmlParser()
        {
            // Html parsing config for AngleSharp : load and interpret Css stylesheets
            var config = Configuration.Default
                .WithDefaultLoader(loaderConfig => { loaderConfig.IsResourceLoadingEnabled = true; loaderConfig.Filter = FilterHtmlAndCssResources; })
                .WithCss(cssConfig => { cssConfig.Options = new AngleSharp.Parser.Css.CssParserOptions() { FilterDisplayAndVisibilityOnly = true }; });

            context = BrowsingContext.New(config);
            context.Parsed += TrackParsedFilesSize; // used to measure perfs

            // DEBUG : uncomment to debug Anglesharp network requests 
            //context.Requested += HtmlParser_Requested;
            //context.Requesting += HtmlParser_Requesting;

            // DEBUG : uncomment to debug Anglesharp parsing process
            //context.Parsing += HtmlParser_Parsing;
            //context.Parsed += HtmlParser_Parsed;
            //context.ParseError += HtmlParser_ParseError;
        }

        private void TrackParsedFilesSize(object sender, AngleSharp.Dom.Events.Event ev)
        {
            if (ev is HtmlParseEvent)
            {
                var textSize = ((HtmlParseEvent)ev).Document.Source.Length;
                Perfs.AddDownloadSize(textSize);
            }
            else if (ev is CssParseEvent)
            {
                var textSize = ((CssParseEvent)ev).StyleSheet.SourceCode.Text.Length;
                Perfs.AddDownloadSize(textSize);
            }
        }        

        // Trick to be able to share the same parsed Html document between Abot and HtmlDocumentConverter
        // We need to activate Css dependencies loading to enable this
        private CrawlDecision WebCrawler_ShouldCrawlPageLinks(CrawledPage crawledPage, CrawlContext crawlContext)
        {
            try
            {
                // Add the page already downloaded by Abot in the document cache
                var htmlDocumentUri = crawledPage.HttpWebResponse.ResponseUri;
                if (!context.ResponseCache.ContainsKey(htmlDocumentUri.AbsoluteUri))
                {
                    var response = VirtualResponse.Create(r =>
                    {
                        r.Address(new Url(htmlDocumentUri.AbsoluteUri))
                            .Status(crawledPage.HttpWebResponse.StatusCode)
                            .Content(crawledPage.Content.Text, crawledPage.Content.Charset);
                        foreach (var header in crawledPage.HttpWebResponse.Headers.AllKeys)
                        {
                            r.Header(header, crawledPage.HttpWebResponse.Headers[header]);
                        }
                    });
                    context.ResponseCache.Add(htmlDocumentUri.AbsoluteUri, response);
                }

                
                Stopwatch timer = Stopwatch.StartNew();
                if (crawledPage.HasHtmlContent)
                {
                    // Parse the page and its Css dependencies whith Anglesharp
                    // in the right context, initialized in the constructor
                    crawledPage.AngleSharpHtmlDocument = context.OpenAsync(htmlDocumentUri.AbsoluteUri).Result as IHtmlDocument;
                }
                else if(crawledPage.HasPdfContent)
                {
                    // Parse the PDF file content
                    crawledPage.PdfDocument = PdfDocument.Open(crawledPage.Content.Bytes);
                }
                timer.Stop();
                Perfs.AddParseTime(timer.ElapsedMilliseconds);

                // Remove page which was just parsed from document cache (not useful anymore)
                context.ResponseCache.Remove(htmlDocumentUri.AbsoluteUri);

                // Don't impact the crawl decision
                return new CrawlDecision() { Allow = true };
            }
            catch(Exception e)
            {
                if (e is ArgumentException)
                {
                    // Do nothing if the key already exists : 
                    // - one exception every 15 minutes is better than a systematic lock on each call
                    // - the crawl decision below will properly avoid analyzing the page twice
                }
                else
                {
                    if (crawledPage.HasHtmlContent)
                    {
                        WriteError("Error while parsing the Html page " + crawledPage.HttpWebResponse.ResponseUri.AbsoluteUri, e);
                    }
                    else if (crawledPage.HasPdfContent)
                    {
                        WriteError("Error while parsing the PDF file " + crawledPage.HttpWebResponse.ResponseUri.AbsoluteUri, e);
                    }
                }

                // Don't crawl
                return new CrawlDecision() { Allow = false };
            }
        }

        private void WriteError(string context, Exception e)
        {
            Perfs.AddCrawlError();
            lock (exceptionsWriter)
            {
                exceptionsWriter.WriteLine(DateTime.Now.ToLongTimeString());
                exceptionsWriter.WriteLine(context);
                exceptionsWriter.WriteLine("--------------------");
                exceptionsWriter.WriteLine(e.Message);
                exceptionsWriter.WriteLine(e.StackTrace);
                exceptionsWriter.WriteLine();
                exceptionsWriter.Flush();
            }
        }

        // Utility method to ensure that we load only Css dependencies
        private bool FilterHtmlAndCssResources(IRequest request, INode originator)
        {
            // Load Html documents
            if (originator == null) { return true; }
            // Load Css stylesheets (and their contents) only
            if (originator is IHtmlLinkElement linkElement)
            {
                IElement element = (IElement)originator;
                if (linkElement.Type != null && linkElement.Type.EndsWith("css", StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }
            // Don't load any other type of resource
            return false;
        }

        private void ConfigureStorageDirectories()
        {
            var storageDirectory = new DirectoryInfo(ExtractorParams.StorageDir);
            if (!storageDirectory.Exists)
            {
                storageDirectory.Create();
            }

            string websitePath = HtmlFileUtils.GetWebsitePathFromUri(ExtractorParams.Scope, ExtractorParams.RootUrl);            
            ContentDirectory = new DirectoryInfo(Path.Combine(storageDirectory.FullName, websitePath));
            if (!ContentDirectory.Exists)
            {
                ContentDirectory.Create();
            }
        }

        // Write a log of the main http requests

        private StreamWriter requestsWriter;
        private StreamWriter messagesWriter;
        private StreamWriter exceptionsWriter;
        private DateTime lastCheckpointTime = DateTime.Now;
        private bool userCancelEventReceived = false;

        public static string LogsDirName = "_nlptextdoc";
        public static string ConfigFileName = "config.txt";
        public static string RequestsLogFileName = "requests.log.csv";
        public static string MessagesLogFileName = "messages.log.txt";
        public static string ExceptionsLogFileName = "exceptions.log.txt";
        public static string CheckpointFileName = "checkpoint.bin";

        private void InitLogFiles()
        {
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) =>
            {
                e.Cancel = true;
                userCancelEventReceived = true;
            };

            var logsDirectory = new DirectoryInfo(Path.Combine(ContentDirectory.FullName, LogsDirName));
            if(!logsDirectory.Exists)
            {
                logsDirectory.Create();
            }

            using(var paramsWriter = new StreamWriter(Path.Combine(logsDirectory.FullName, ConfigFileName), DoContinue))
            {
                if (DoContinue) paramsWriter.WriteLine();
                ExtractorParams.WriteToFile(paramsWriter);
            }

            requestsWriter = new StreamWriter(Path.Combine(logsDirectory.FullName, RequestsLogFileName), DoContinue);
            requestsWriter.Write("Clock");
            requestsWriter.Write(";");
            requestsWriter.Write("Url");
            requestsWriter.Write(";");
            requestsWriter.Write("Status code");
            requestsWriter.Write(";");
            requestsWriter.Write("Reponse time (ms)");
            requestsWriter.Write(";");
            requestsWriter.Write("Download time (ms)");
            requestsWriter.Write(";");
            requestsWriter.Write("Content size (bytes)");
            requestsWriter.Write(";");
            requestsWriter.Write("Unique text blocks (%)");
            requestsWriter.Write(";");
            requestsWriter.Write("Crawl depth");
            requestsWriter.Write(";");
            requestsWriter.Write("Parent Url");
            requestsWriter.Write(";");
            requestsWriter.Write("Redirected from");
            requestsWriter.Write(";");
            requestsWriter.Write("Retry count");
            requestsWriter.Write(";");
            requestsWriter.Write("Retry after (s)");
            requestsWriter.Write(";");
            requestsWriter.Write("Error message");
            requestsWriter.WriteLine();

            messagesWriter = new StreamWriter(Path.Combine(logsDirectory.FullName, MessagesLogFileName), DoContinue);

            exceptionsWriter = new StreamWriter(Path.Combine(logsDirectory.FullName, ExceptionsLogFileName), DoContinue);
            log4net.LogManager.SetTextWriter(exceptionsWriter);
        }

        private void LogRequest(CrawledPage crawledPage, float percentUnique)
        {
            lock (requestsWriter)
            {
                requestsWriter.Write(crawledPage.RequestStarted.ToString("HH:mm:ss.fff"));
                requestsWriter.Write(";");
                requestsWriter.Write(ToCsvSafeString(crawledPage.Uri.AbsoluteUri));
                requestsWriter.Write(";");
                if (crawledPage.HttpWebResponse != null)
                {
                    requestsWriter.Write(crawledPage.HttpWebResponse.StatusCode);
                }
                requestsWriter.Write(";");
                requestsWriter.Write((int)crawledPage.Elapsed);
                if (crawledPage.DownloadContentCompleted.HasValue)
                {
                    requestsWriter.Write(";");
                    requestsWriter.Write((int)(crawledPage.DownloadContentCompleted.Value - crawledPage.DownloadContentStarted.Value).TotalMilliseconds);
                    requestsWriter.Write(";");
                    requestsWriter.Write(crawledPage.Content.Bytes.Length);
                    requestsWriter.Write(";");
                    requestsWriter.Write(percentUnique);
                }
                else
                {
                    requestsWriter.Write(";");
                    requestsWriter.Write(";");
                    requestsWriter.Write(";");
                }
                requestsWriter.Write(";");
                requestsWriter.Write(crawledPage.CrawlDepth);
                requestsWriter.Write(";");
                requestsWriter.Write(crawledPage.ParentUri != null ? ToCsvSafeString(crawledPage.ParentUri.AbsoluteUri) : "");
                requestsWriter.Write(";");
                requestsWriter.Write(crawledPage.RedirectedFrom != null ? ToCsvSafeString(crawledPage.RedirectedFrom.Uri.AbsoluteUri) : "");
                if (crawledPage.IsRetry)
                {
                    requestsWriter.Write(";");
                    requestsWriter.Write(crawledPage.RetryCount);
                    requestsWriter.Write(";");
                    requestsWriter.Write(crawledPage.RetryAfter.Value);
                }
                else
                {
                    requestsWriter.Write(";");
                    requestsWriter.Write(";");
                }
                if (crawledPage.WebException != null)
                {
                    requestsWriter.Write(";");
                    requestsWriter.Write(ToCsvSafeString(crawledPage.WebException.Message));
                }
                else
                {
                    requestsWriter.Write(";");
                }
                requestsWriter.WriteLine();
                requestsWriter.Flush();
            }
        }

        private static string ToCsvSafeString(string message)
        {
            return message.Replace(';', ',').Replace('\n', ' ');
        }

        public void Dispose()
        {
            requestsWriter.Dispose();
            requestsWriter = null;

            messagesWriter.Dispose();
            messagesWriter = null;

            exceptionsWriter.Dispose();
            exceptionsWriter = null;
        }

        /// <summary>
        /// Crawl all pages of the website and convert them to NLPTextDocuments
        /// </summary>
        public void ExtractNLPTextDocuments()
        {
            Perfs = new PerfMonitor();

            DisplayMessages(WriteStartMessage);
            DisplayMessages(Perfs.WriteStatusHeader);

            // This is synchronous, it will not go to the next line until the crawl has completed
            CrawlResult result = crawler.Crawl(ExtractorParams.RootUrl);
            Perfs.EndTime = DateTime.Now;

            // Write end status to log file
            Perfs.WriteStatus(messagesWriter);

            string endMessage = null;
            if (result.ErrorOccurred)
                endMessage = "Extraction completed with fatal error \"" + result.ErrorException.Message + "\"";
            else
                endMessage = "Extraction completed";
            DisplayMessages(WriteEndMessage, endMessage);
        }
               
        private delegate void WriteMessage(TextWriter wr, string message);

        private void DisplayMessages(WriteMessage writeMessage, string message = null)
        {
            writeMessage(Console.Out, message);
            writeMessage(messagesWriter, message);
            messagesWriter.Flush();
        }

        private void WriteStartMessage(TextWriter wr, string message = null)
        {
            wr.WriteLine(DateTime.Now.ToString() + " : nlptextdoc extraction " + (DoContinue ? "continued from previous execution" : "started"));
            wr.WriteLine();
            wr.WriteLine(">>> From : " + ExtractorParams.RootUrl);
            wr.WriteLine(">>> To   : " + ContentDirectory);
            wr.WriteLine();
        }

        private static void WriteEndMessage(TextWriter wr, string endMessage)
        {
            wr.WriteLine();
            wr.WriteLine();
            wr.WriteLine(DateTime.Now.ToString() + " : " + endMessage);
            wr.WriteLine();
        }

        // => called each time a page has been crawled by the web crawler
        private void WebCrawler_PageCrawlCompletedAsync(object sender, PageCrawlCompletedArgs e)
        {
            try
            {
                CrawledPage crawledPage = e.CrawledPage;

                // Exit if the page wasn't crawled successfully
                if (crawledPage.WebException != null || crawledPage.HttpWebResponse.StatusCode != HttpStatusCode.OK)
                {
                    LogRequest(crawledPage, 0);
                    
                    if (crawledPage.WebException != null)
                    {
                        var message = crawledPage.WebException.Message.ToLower();
                        if (!message.Contains("not found") && !message.Contains("moved"))
                        {
                            Perfs.AddCrawlError();
                        }
                    }
                    else if(crawledPage.HttpWebResponse != null)
                    {
                        int statusCode = (int)crawledPage.HttpWebResponse.StatusCode;
                        if(statusCode != 404 && statusCode >= 400)
                        {
                            Perfs.AddCrawlError();
                        }
                    }
                    return;
                }

                // Exit if the page had non content
                if (string.IsNullOrEmpty(crawledPage.Content.Text))
                {
                    LogRequest(crawledPage, 0);
                    return;
                }

                Stopwatch timer = Stopwatch.StartNew();
                NLPTextDocument normalizedTextDocument = null;
                var htmlDocumentUri = crawledPage.HttpWebResponse.ResponseUri;
                if (crawledPage.HasHtmlContent)
                {
                    // Get the page and its Css dependencies parsed by Abot whith Anglesharp
                    var htmlDocument = crawledPage.AngleSharpHtmlDocument;

                    // Visit the Html page syntax tree and convert it to NLPTextDocument                
                    var htmlConverter = new HtmlDocumentConverter(htmlDocumentUri.AbsoluteUri, htmlDocument);
                    normalizedTextDocument = htmlConverter.ConvertToNLPTextDocument();
                }
                else if (crawledPage.HasPdfContent)                    
                {
                    // Get the PDF file content parsed by PdfPig
                    var pdfDocument = crawledPage.PdfDocument;

                    // Analyze the layout of the Pdf file and extract text blocks
                    // - one section per page
                    // - one multiline textblock per block
                    normalizedTextDocument = PdfDocumentConverter.ConvertToNLPTextDocument(htmlDocumentUri.AbsoluteUri, pdfDocument);
                }
                timer.Stop();

                // Check the percentage of text blocks which are new & unique in this page
                var percentUnique = Perfs.SetPercentUniqueForLastDoc(normalizedTextDocument);
                
                // Log the request results
                LogRequest(crawledPage, percentUnique);

                // Write the NLPTextDocument as a text file on disk
                if (percentUnique > 0)
                {
                    var fileInfo = HtmlFileUtils.GetFilePathFromUri(ContentDirectory, htmlDocumentUri);
                    if (!fileInfo.Directory.Exists)
                    {
                        fileInfo.Directory.Create();
                    }
                    NLPTextDocumentWriter.WriteToFile(normalizedTextDocument, fileInfo.FullName);

                    Perfs.AddTextConversion(timer.ElapsedMilliseconds, fileInfo.Length);
                }

                // Test stopping conditions
                bool stopCrawl = false;
                string stopMessage = null;
                if(userCancelEventReceived)
                {
                    stopCrawl = true;
                    stopMessage = "Extraction interrupted by the user";
                }
                else if (ExtractorParams.MaxDuration > 0 && TimeSpan.FromMilliseconds(Perfs.ElapsedTime).Minutes >= ExtractorParams.MaxDuration)
                {
                    stopCrawl = true;
                    stopMessage = "Extraction stopped because the extraction duration exceeded " + ExtractorParams.MaxDuration + " minutes";
                }                               
                else if(ExtractorParams.MaxPageCount > 0 && Perfs.HtmlPagesCount >= ExtractorParams.MaxPageCount)
                {
                    stopCrawl = true;
                    stopMessage = "Extraction stopped because the number of extracted pages exceeded " + ExtractorParams.MaxPageCount;
                }
                else if (ExtractorParams.MaxErrorsCount > 0 && Perfs.CrawlErrorsCount >= ExtractorParams.MaxErrorsCount)
                {
                    stopCrawl = true;
                    stopMessage = "Extraction stopped because the number of crawl errors exceeded " + ExtractorParams.MaxErrorsCount;
                }
                else if (ExtractorParams.MinUniqueText > 0 && Perfs.PercentUniqueForLastDocs < (ExtractorParams.MinUniqueText / 100.0))
                {
                    stopCrawl = true;
                    stopMessage = "Extraction stopped because the % of new textblocks fell below " + ExtractorParams.MinUniqueText + "%";
                }
                else if (ExtractorParams.MaxSizeOnDisk > 0 && Perfs.TotalSizeOnDisk >= (ExtractorParams.MaxSizeOnDisk * 1024L * 1024L))
                {
                    stopCrawl = true;
                    stopMessage = "Extraction stopped because the files size on disk exceeded " + ExtractorParams.MaxSizeOnDisk + " MB";
                }

                // Write current status to screen (and to file if stopping)
                if (!stopCrawl)
                {
                    Perfs.WriteStatus(Console.Out);
                }
                else
                {
                    DisplayMessages(Perfs.WriteStatus);
                }

                // Write one checkpoint every one minute 
                // to enable the "continue" crawl feature
                lock (CheckpointFileName)
                {
                    if (stopCrawl || DateTime.Now.Subtract(lastCheckpointTime).Minutes >= 1)
                    {
                        lastCheckpointTime = DateTime.Now;
                        using (FileStream fs = new FileStream(Path.Combine(ContentDirectory.FullName, LogsDirName, CheckpointFileName), FileMode.Create))
                        {
                            scheduler.Serialize(fs);
                        }
                    }

                    if (stopCrawl)
                    {
                        DisplayMessages(WriteEndMessage, stopMessage);
                        Environment.Exit(0);
                    }
                }                
            }
            catch (Exception ex)
            {
                // Safeguard to make sure that an error 
                // during the processing of a single page 
                // can't stop the whole crawl process                
                WriteError("Error while processing the page : " + e.CrawledPage.HttpWebResponse.ResponseUri.AbsoluteUri,  ex);
            }
        }        

        // -----------------------
        // DEBUG output statements
        // -----------------------

        private static void WebCrawler_PageCrawlStartingAsync(object sender, PageCrawlStartingArgs e)
        {
            PageToCrawl pageToCrawl = e.PageToCrawl;
            Console.WriteLine("Abot-About to crawl link {0} which was found on page {1}", pageToCrawl.Uri.AbsoluteUri, pageToCrawl.ParentUri.AbsoluteUri);
        }

        private static void WebCrawler_PageCrawlDisallowedAsync(object sender, PageCrawlDisallowedArgs e)
        {
            PageToCrawl pageToCrawl = e.PageToCrawl;
            Console.WriteLine("Abot-Did not crawl page {0} due to {1}", pageToCrawl.Uri.AbsoluteUri, e.DisallowedReason);
        }

        private static void WebCrawler_PageLinksCrawlDisallowedAsync(object sender, PageLinksCrawlDisallowedArgs e)
        {
            CrawledPage crawledPage = e.CrawledPage;
            Console.WriteLine("Abot-Did not crawl the links on page {0} due to {1}", crawledPage.Uri.AbsoluteUri, e.DisallowedReason);
        }

        private void HtmlParser_Requesting(object sender, Event ev)
        {
            Console.WriteLine("AngleSharp-Requesting: " + ((RequestEvent)ev).Request.Address);
        }

        private void HtmlParser_Requested(object sender, Event ev)
        {
            Console.WriteLine("AngleSharp-Requested: " + ((RequestEvent)ev).Response.StatusCode + " (" + ((RequestEvent)ev).Request.Address + ")");
        }

        private void HtmlParser_Parsing(object sender, AngleSharp.Dom.Events.Event ev)
        {
            if (ev is HtmlParseEvent)
            {
                Console.WriteLine("AngleSharp-Parsing: " + ((HtmlParseEvent)ev).Document.Url);
            }
            else if (ev is CssParseEvent)
            {
                var cssSource = ((CssParseEvent)ev).StyleSheet.Href;
                if (String.IsNullOrEmpty(cssSource))
                {
                    cssSource = ((CssParseEvent)ev).StyleSheet.OwnerNode.LocalName;
                }
                Console.WriteLine("AngleSharp-Parsing: " + cssSource);
            }
        }

        private void HtmlParser_Parsed(object sender, AngleSharp.Dom.Events.Event ev)
        {
            if (ev is HtmlParseEvent)
            {
                Console.WriteLine("AngleSharp-Parsed: " + ((HtmlParseEvent)ev).Document.Source.Length + " HTML chars (" + ((HtmlParseEvent)ev).Document.Url + ")");
            }
            else if (ev is CssParseEvent)
            {
                var cssSource = ((CssParseEvent)ev).StyleSheet.Href;
                if (String.IsNullOrEmpty(cssSource))
                {
                    cssSource = ((CssParseEvent)ev).StyleSheet.OwnerNode.LocalName;
                }
                Console.WriteLine("AngleSharp-Parsed: " + ((CssParseEvent)ev).StyleSheet.Children.Count() + " CSS rules (" + cssSource + ")");
            }
        }

        private void HtmlParser_ParseError(object sender, Event ev)
        {
            if (ev is HtmlErrorEvent)
            {
                Console.WriteLine("AngleSharp-ParseERROR: line " + ((HtmlErrorEvent)ev).Position.Line + ": " + ((HtmlErrorEvent)ev).Message);
            }
            else if (ev is CssErrorEvent)
            {
                Console.WriteLine("AngleSharp-ParseERROR: line " + ((CssErrorEvent)ev).Position.Line + ": " + ((CssErrorEvent)ev).Message);
            }
        }

        public class PerfMonitor
        {
            public PerfMonitor()
            {
                StartTime = DateTime.Now;
                for(int i = 0; i < percentUniqueForLastDocs.Length; i++)
                {
                    percentUniqueForLastDocs[i] = -1;
                }
            }

            // Count converted Html pages only
            public int HtmlPagesCount;
            public int CrawlErrorsCount;

            // Bytes
            public long TotalDownloadSize;
            public long TotalSizeOnDisk;

            // Milliseconds

            public long HtmlParseTime;
            public long TextConvertTime;

            // Track unique text blocks
            private HashSet<int> stringHashes = new HashSet<int>();
            float[] percentUniqueForLastDocs = new float[1000];
            int lastDocIndex = -1;

            internal float SetPercentUniqueForLastDoc(NLPTextDocument document)
            {
                lock (stringHashes)
                {
                    int charCount = 0;
                    int uniqueCharCount = 0;
                    foreach (var str in document.TextStrings)
                    {
                        charCount += str.Length;
                        var hashCode = str.GetHashCode();
                        if (!stringHashes.Contains(hashCode))
                        {
                            stringHashes.Add(hashCode);
                            uniqueCharCount += str.Length;
                        }
                    }
                    var percent = (charCount > 0) ? (uniqueCharCount / (float)charCount) : 1;

                    lastDocIndex++;
                    if (lastDocIndex >= percentUniqueForLastDocs.Length)
                    {
                        lastDocIndex = 0;
                    }
                    percentUniqueForLastDocs[lastDocIndex] = percent;

                    return percent;
                }
            }

            public float PercentUniqueForLastDocs
            {
                get
                {
                    float sum = 0;
                    int count = 0;
                    for(int i = 0; i < percentUniqueForLastDocs.Length; i++)
                    {
                        var percent = percentUniqueForLastDocs[i];
                        if (percent < 0) break;
                        sum += percent;
                        count++;
                    }
                    if(count == 0)
                    {
                        return 1;
                    }
                    else
                    {
                        return sum / count;
                    }
                }
            }

            internal void AddCrawlError()
            {
                Interlocked.Increment(ref CrawlErrorsCount);
            }

            public void AddDownloadSize(int downloadSize)
            {
                Interlocked.Add(ref TotalDownloadSize, downloadSize);
            }

            public void AddParseTime(long parseTime)
            {
                Interlocked.Add(ref HtmlParseTime, parseTime);
            }

            public void AddTextConversion(long conversionTime, long sizeOnDisk)
            {
                Interlocked.Increment(ref HtmlPagesCount);
                Interlocked.Add(ref TextConvertTime, conversionTime);
                Interlocked.Add(ref TotalSizeOnDisk, sizeOnDisk);
            }

            public DateTime StartTime;
            public DateTime EndTime;

            public long ElapsedTime
            {
                get
                {
                    return (EndTime != DateTime.MinValue) ?
                        (long)(EndTime - StartTime).TotalMilliseconds :
                        (long)(DateTime.Now - StartTime).TotalMilliseconds;
                }
            }

            public void WriteStatusHeader(TextWriter wr, string message = null)
            {
                wr.WriteLine("Time    | Pages | Errors | Unique  | Download   | Disk       | Parsing | Convert |");
            }

            public void WriteStatus(TextWriter wr, string message = null)
            {
                wr.Write("\r{0} | {1,5} | {2,5}  |  {3,3} %  | {4,7:0.0} Mb | {5,7:0.0} Mb | {6} | {7} |",
                    TimeSpan.FromMilliseconds(ElapsedTime).ToString(@"h\:mm\:ss"),
                    HtmlPagesCount,
                    CrawlErrorsCount,
                    (int)(PercentUniqueForLastDocs*100),
                    TotalDownloadSize / 1024.0 / 1024.0,
                    TotalSizeOnDisk / 1024.0 / 1024.0,
                    TimeSpan.FromMilliseconds(HtmlParseTime).ToString(@"h\:mm\:ss"),
                    TimeSpan.FromMilliseconds(TextConvertTime).ToString(@"h\:mm\:ss"));
            }
        }
    }
}

