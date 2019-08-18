using System;

namespace Robots.Model
{
    public abstract class UrlEntry : Entry
    {
        protected UrlEntry(EntryType type)
            : base(type)
        { }
  
        public string Pattern { get; set; }
    }
}