using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nlptextdoc.image
{
    public enum BoundingBoxType
    {
        TextBlock,
        TextLine,
        Word,
        Char
    }

    public class PageElement
    {
        public string tagName;
        public string classNames;
        public BoundingBox boundingBox;
        public PageElement[] children;

        // TextBlock and TextLabel
        public string text;
        // TextBlock
        public TextLine[] lines;
    
        public bool IsTextBlock { get { return lines != null; } }
        public bool IsTextLabel { get { return lines == null && text != null; } }
    }

    public class TextLine
    {
        public string text;
        public BoundingBox boundingBox;
        public Word[] words;
    }

    public class Word
    {
        public string text;
        public BoundingBox boundingBox;
        public Letter[] letters;
    }

    public class Letter
    {
        public char @char;
        public BoundingBox boundingBox;
    }

    public class BoundingBox
    {
        public int x;
        public int y;
        public int width;
        public int height;
    }
}
