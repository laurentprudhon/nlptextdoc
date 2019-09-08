using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Abot.Core
{
    /// <summary>
    /// Implementation that stores a numeric hash of the url instead of the url itself to use for lookups. This should save space when the crawled url list gets very long. 
    /// </summary>
    [Serializable]
    public class CompactCrawledUrlRepository : ICrawledUrlRepository, ISerializable
    {
        private ConcurrentDictionary<long, byte> m_UrlRepository = new ConcurrentDictionary<long, byte>();

        public CompactCrawledUrlRepository() { }

        /// <inheritDoc />
        public bool Contains(Uri uri)
        {
            return m_UrlRepository.ContainsKey(ComputeNumericId(uri.AbsoluteUri));
        }

        /// <inheritDoc />
        public bool AddIfNew(Uri uri)
        {
            return m_UrlRepository.TryAdd(ComputeNumericId(uri.AbsoluteUri), 0);
        }

        /// <inheritDoc />
        public virtual void Dispose()
        {
            m_UrlRepository = null;
        }

        protected long ComputeNumericId(string p_Uri)
        {
            byte[] md5 = ToMd5Bytes(p_Uri);

            long numericId = 0;
            for (int i = 0; i < 8; i++)
            {
                numericId += (long)md5[i] << (i * 8);
            }

            return numericId;
        }

        protected byte[] ToMd5Bytes(string p_String)
        {
            using (MD5 md5 = MD5.Create())
            {
                return md5.ComputeHash(Encoding.Default.GetBytes(p_String));
            }
        }

        private CompactCrawledUrlRepository(SerializationInfo info, StreamingContext context)
        {            
            int count = info.GetInt32("cnt");
            for (int i = 0; i < count; i++)
            {
                long key = info.GetInt64("k" + i);
                m_UrlRepository.TryAdd(key, 0);
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("cnt", m_UrlRepository.Count);
            int i = 0;
            foreach (long key in m_UrlRepository.Keys)
            {
                info.AddValue("k" + i, key);
                i++;
            }
        }
    }
}
