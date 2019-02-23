using System;
using System.Collections.Generic;
using System.Globalization;

namespace nlptextdoc.text.document
{
    /// <summary>
    /// Use this class to build a NLPTextDocument element by element
    /// while parsing a Html file, a PDF document, or a Wikipedia dump :
    /// maintains state while opening and closing nested sections and lists.
    /// </summary>
    public class NLPTextDocumentBuilder
    {
        public NLPTextDocument TextDocument { get; private set; }

        private Stack<DocumentElementType> containersType;
        private Stack<IList<DocumentElement>> containers;
        private int NestingLevel { get { return containers.Count; } }
        private DocumentElementType CurrentContainerType { get { return containersType.Peek(); } }
        private IList<DocumentElement> CurrentContainer { get { return containers.Peek(); } }

        public NLPTextDocumentBuilder(string uri = "?")
        {
            TextDocument = new NLPTextDocument(uri);
            containersType = new Stack<DocumentElementType>();
            containersType.Push(DocumentElementType.Section);
            containers = new Stack<IList<DocumentElement>>();
            containers.Push(TextDocument.Elements);
        }

        public void SetTitle(string title)
        {
            TextDocument.Title = title;
        }

        public void SetUri(string uri)
        {
            TextDocument.Uri = uri;
        }

        public void SetTimestamp(string timestamp)
        {
            TextDocument.Timestamp = DateTime.Parse(timestamp, CultureInfo.InvariantCulture);
        }

        public void SetTimestamp(DateTime timestamp)
        {
            TextDocument.Timestamp = timestamp;
        }

        public void AddMetadata(string key, string value)
        {
            TextDocument.Metadata.Add(key, value);
        }

        public void AddTextBlock(string text)
        {
            if( CurrentContainerType != DocumentElementType.List && 
                CurrentContainerType != DocumentElementType.NavigationList && 
                CurrentContainerType != DocumentElementType.Table)
            {
                var docElt = new TextBlock(NestingLevel, text);
                CurrentContainer.Add(docElt);
            }
        }

        public void StartSection(string title = null)
        {
            var docElt = new Section(NestingLevel, title);
            CurrentContainer.Add(docElt);
            containersType.Push(docElt.Type);
            containers.Push(docElt.Elements);
        }

        public void EndSection()
        {
            // TO DO : check type at the top of the stack !
            containersType.Pop();
            containers.Pop();
        }

        public void StartNavigationList(string title = null)
        {
            var docElt = new List(NestingLevel, true, title);
            CurrentContainer.Add(docElt);
            containersType.Push(docElt.Type);
            containers.Push(docElt.Elements);
        }

        public void StartList(string title = null)
        {
            var docElt = new List(NestingLevel, title);
            CurrentContainer.Add(docElt);
            containersType.Push(docElt.Type);
            containers.Push(docElt.Elements);
        }

        public void EndList()
        {
            // TO DO : check type at the top of the stack !
            containersType.Pop();
            containers.Pop();
        }

        public void StartListItem()
        {
            var docElt = new ListItem(NestingLevel);
            CurrentContainer.Add(docElt);
            containersType.Push(docElt.Type);
            containers.Push(docElt.Elements);
        }

        public void EndListItem()
        {
            // TO DO : check type at the top of the stack !
            containersType.Pop();
            containers.Pop();
        }

        public void StartTable(string title = null)
        {
            var docElt = new Table(NestingLevel, title);
            CurrentContainer.Add(docElt);
            containersType.Push(docElt.Type);
            containers.Push((IList<DocumentElement>)docElt.Elements);
        }

        public void EndTable()
        {
            // TO DO : check type at the top of the stack !
            containersType.Pop();
            containers.Pop();
        }

        public void StartTableHeader(int row, int col, int rowspan = 1, int colspan = 1)
        {
            var docElt = new TableHeader(NestingLevel, row, rowspan, col, colspan);
            CurrentContainer.Add(docElt);
            containersType.Push(docElt.Type);
            containers.Push(docElt.Elements);
        }

        public void EndTableHeader()
        {
            // TO DO : check type at the top of the stack !
            containersType.Pop();
            containers.Pop();
        }

        public void StartTableCell(int row, int col, int rowspan = 1, int colspan = 1)
        {
            var docElt = new TableCell(NestingLevel, row, rowspan, col, colspan);
            CurrentContainer.Add(docElt);
            containersType.Push(docElt.Type);
            containers.Push(docElt.Elements);
        }

        public void EndTableCell()
        {
            // TO DO : check type at the top of the stack !
            containersType.Pop();
            containers.Pop();
        }
    }
}
