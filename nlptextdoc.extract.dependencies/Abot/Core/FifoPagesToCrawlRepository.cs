using Abot.Poco;
using System;
using System.Collections.Concurrent;
using System.Dynamic;
using System.Runtime.Serialization;

namespace Abot.Core
{
    public interface IPagesToCrawlRepository : IDisposable
    {
        void Add(PageToCrawl page);
        PageToCrawl GetNext();
        void Clear();
        int Count();

    }

    [Serializable]
    public class FifoPagesToCrawlRepository : IPagesToCrawlRepository, ISerializable
    {
        ConcurrentQueue<PageToCrawl> _urlQueue = new ConcurrentQueue<PageToCrawl>();

        public FifoPagesToCrawlRepository() { }

        public void Add(PageToCrawl page)
        {
            _urlQueue.Enqueue(page);
        }

        public PageToCrawl GetNext()
        {
            PageToCrawl pageToCrawl;
            _urlQueue.TryDequeue(out pageToCrawl);

            return pageToCrawl;
        }

        public void Clear()
        {
            _urlQueue = new ConcurrentQueue<PageToCrawl>();
        }

        public int Count()
        {
            return _urlQueue.Count;
        }

        public virtual void Dispose()
        {
            _urlQueue = null;
        }

        private FifoPagesToCrawlRepository(SerializationInfo info, StreamingContext context)
        {
            int count = info.GetInt32("cnt");
            for (int i = 0; i < count; i++)
            {
                PageToCrawl page = (PageToCrawl)info.GetValue("p" + i, typeof(PageToCrawl));
                page.PageBag = new ExpandoObject();
                _urlQueue.Enqueue(page);
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("cnt", _urlQueue.Count);
            int i = 0;
            foreach (PageToCrawl page in _urlQueue)
            {
                info.AddValue("p" + i,page);
                i++;
            }
        }
    }

}
