using Abot.Poco;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Abot.Core
{
    /// <summary>
    /// Handles managing the priority of what pages need to be crawled
    /// </summary>
    public interface IScheduler : IDisposable
    {
        /// <summary>
        /// Count of remaining items that are currently scheduled
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Schedules the param to be crawled
        /// </summary>
        void Add(PageToCrawl page);

        /// <summary>
        /// Schedules the param to be crawled
        /// </summary>
        void Add(IEnumerable<PageToCrawl> pages);

        /// <summary>
        /// Gets the next page to crawl
        /// </summary>
        PageToCrawl GetNext();

        /// <summary>
        /// Clear all currently scheduled pages
        /// </summary>
        void Clear();

        /// <summary>
        /// Add the Url to the list of crawled Url without scheduling it to be crawled.
        /// </summary>
        /// <param name="uri"></param>
        void AddKnownUri(Uri uri);

        /// <summary>
        /// Returns whether or not the specified Uri was already scheduled to be crawled or simply added to the
        /// list of known Uris.
        /// </summary>
        bool IsUriKnown(Uri uri);
    }

    [Serializable]
    public class Scheduler : IScheduler
    {
        ICrawledUrlRepository _crawledUrlRepo;
        IPagesToCrawlRepository _pagesToCrawlRepo;
        bool _allowUriRecrawling;

        public Scheduler()
            :this(false, null, null)
        {
        }

        public Scheduler(bool allowUriRecrawling, ICrawledUrlRepository crawledUrlRepo, IPagesToCrawlRepository pagesToCrawlRepo)
        {
            _allowUriRecrawling = allowUriRecrawling;
            _crawledUrlRepo = crawledUrlRepo ?? new CompactCrawledUrlRepository();
            _pagesToCrawlRepo = pagesToCrawlRepo ?? new FifoPagesToCrawlRepository();
        }

        public static Scheduler Deserialize(Stream fs)
        {
            Scheduler scheduler = null;
            try
            {
                BinaryFormatter formatter = new BinaryFormatter();
                scheduler = (Scheduler)formatter.Deserialize(fs);
            }
            catch (SerializationException e)
            {
                throw new Exception("Failed to deserialize Scheduler. Reason: " + e.Message);
            }
            finally
            {
                fs.Close();
            }
            return scheduler;
        }

        public delegate bool PageFilter(PageToCrawl pageToCrawl);

        public void FilterAllowedUrlsAfterConfig(PageFilter shouldCrawlPage)
        {
            // The Scheduler was deserialized after a 'continue' command
            if(_pagesToCrawlRepo?.Count()  > 0)
            {
                var initialRepo = _pagesToCrawlRepo;
                _pagesToCrawlRepo = new FifoPagesToCrawlRepository();
                PageToCrawl candidatePage = null;
                while ((candidatePage = initialRepo.GetNext()) != null)
                {
                    if(shouldCrawlPage(candidatePage))
                    {
                        _pagesToCrawlRepo.Add(candidatePage);
                    }
                }
            }            
        }

        public void Serialize(Stream fs)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            try
            {
                formatter.Serialize(fs, this);
            }
            catch (SerializationException e)
            {
                throw new Exception("Failed to serialize Scheduler. Reason: " + e.Message);                
            }
            finally
            {
                fs.Close();
            }
        }

        public int Count
        {
            get { return _pagesToCrawlRepo.Count(); }
        }

        public void Add(PageToCrawl page)
        {
            if (page == null)
                throw new ArgumentNullException("page");

            if (_allowUriRecrawling || page.IsRetry)
            {
                _pagesToCrawlRepo.Add(page);
            }
            else
            {
                if (_crawledUrlRepo.AddIfNew(page.Uri))
                    _pagesToCrawlRepo.Add(page);
            }
        }

        public void Add(IEnumerable<PageToCrawl> pages)
        {
            if (pages == null)
                throw new ArgumentNullException("pages");

            foreach (PageToCrawl page in pages)
                Add(page);
        }

        public PageToCrawl GetNext()
        {
            return _pagesToCrawlRepo.GetNext();
        }

        public void Clear()
        {
            _pagesToCrawlRepo.Clear();
        }

        public void AddKnownUri(Uri uri)
        {
            _crawledUrlRepo.AddIfNew(uri);
        }

        public bool IsUriKnown(Uri uri)
        {
            return _crawledUrlRepo.Contains(uri);
        }

        public void Dispose()
        {
            if (_crawledUrlRepo != null)
            {
                _crawledUrlRepo.Dispose();
            }
            if (_pagesToCrawlRepo != null)
            {
                _pagesToCrawlRepo.Dispose();
            }
        }
    }
}
