using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace nlptextdoc.text.document
{
    /// <summary>
    /// Write a NLPTextDocument to a text file on disk
    /// </summary>
    public static class NLPTextDocumentWriter
    {
        public static void WriteToFile(NLPTextDocument doc, string path)
        {
            using (var writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                int lastNestingLevel = 0;

                WriteDocumentProperty(writer, NLPTextDocumentFormat.TEXT_DOCUMENT_TITLE, doc.Title);
                WriteDocumentProperty(writer, NLPTextDocumentFormat.TEXT_DOCUMENT_URI, doc.Uri);
                WriteDocumentProperty(writer, NLPTextDocumentFormat.TEXT_DOCUMENT_TIMESTAMP, doc.Timestamp.ToString(CultureInfo.InvariantCulture));
                if (doc.HasMetadata)
                {
                    foreach (var key in doc.Metadata.Keys)
                    {
                        WriteDocumentProperty(writer, NLPTextDocumentFormat.TEXT_DOCUMENT_METADATA, key + "=" + doc.Metadata[key]);
                    }
                }
                writer.WriteLine();
                WriteDocumentElements(writer, doc.Elements);
            }
        }

        // ## NLPTextDocument Title ...value...
        // ## NLPTextDocument Uri ...value...
        // ## NLPTextDocument Timestamp ...value...
        // ## NLPTextDocument Metadata [key]=...value..
        private static void WriteDocumentProperty(StreamWriter writer, string propertyName, string propertyValue)
        {
            writer.Write(NLPTextDocumentFormat.TEXT_DOCUMENT_PROPERTY_PREFIX);
            writer.Write(propertyName);
            writer.Write(' ');
            if (!String.IsNullOrEmpty(propertyValue))
            {
                WriteTextBlock(writer, propertyValue);
            }
            else
            {
                writer.WriteLine();
            }
        }

        private static void WriteDocumentElements(StreamWriter writer, IEnumerable<DocumentElement> elements)
        {
            foreach (var docElement in elements)
            {
                if (docElement.Type == DocumentElementType.TextBlock)
                {
                    var textBlock = (TextBlock)docElement;
                    WriteTextBlock(writer, textBlock.Text);
                }
                else
                {
                    var groupElement = docElement as GroupElement;
                    if (groupElement != null) // always true
                    {
                        bool skipGroupWrapper = false;
                        // Always write NavigationLists (type is a valuable info in this case)
                        if (docElement.Type != DocumentElementType.NavigationList)
                        {
                            // Always write group element wrapper if it has a title
                            var groupElementWithTitle = docElement as GroupElementWithTitle;
                            if (groupElementWithTitle == null || !groupElementWithTitle.HasTitle)
                            {
                                // Skip group wrapper when the group is empty 
                                skipGroupWrapper = groupElement.IsEmpty;
                            }
                        }

                        if (skipGroupWrapper)
                        {
                            if (!groupElement.IsEmpty)
                            {
                                WriteDocumentElements(writer, groupElement.Elements);
                            }
                        }
                        else
                        {
                            // Write the list items on one line for readability 
                            // if they are sufficiently "short"
                            var listElement = groupElement as List;
                            bool writeGroupOnOneLine = listElement != null && listElement.IsShort;
                            if (writeGroupOnOneLine)
                            {
                                WriteListItems(writer, listElement);
                            }
                            else
                            {
                                WriteDocumentElementStart(writer, docElement);
                                WriteDocumentElements(writer, groupElement.Elements);
                                WriteDocumentElementEnd(writer, docElement);
                            }
                        }
                    }
                }
            }
        }

        // ## [NestingLevel] [DocumentElementName] Items [Title] >> [item 1] || [item 2] || [item 3]
        private static void WriteListItems(StreamWriter writer, List listElement)
        {
            writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_LINE_MARKER);
            writer.Write(' ');
            writer.Write(listElement.NestingLevel);
            writer.Write(' ');
            writer.Write(NLPTextDocumentFormat.ElemNameForElemType[listElement.Type]);
            writer.Write(' ');
            writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_ITEMS);
            writer.Write(' ');
            if (listElement.HasTitle)
            {
                writer.Write(((GroupElementWithTitle)listElement).Title);
                writer.Write(' ');
            }
            writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_ITEMS_START);
            writer.Write(' ');
            var items = listElement.Elements.Select(item => ((ListItem)item).Elements.OfType<TextBlock>().FirstOrDefault());
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (item != null)
                {
                    if (!isFirstItem)
                    {
                        writer.Write(' ');
                        writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_ITEMS_SEPARATOR);
                        writer.Write(' ');
                    }
                    else
                    {
                        isFirstItem = false;
                    }
                    WriteTextBlock(writer, item.Text, false);
                }
            }
            writer.WriteLine();
        }

        // ## [NestingLevel] [Section|List|Table] Start ...title...
        // ## [NestingLevel] ListItem Start
        // ## [NestingLevel] [TableHeader|TableCell] Start row,col
        // ## [NestingLevel] [TableHeader|TableCell] Start row:rowspan,col:colspan
        private static void WriteDocumentElementStart(StreamWriter writer, DocumentElement docElement)
        {
            if (docElement.Type == DocumentElementType.Section || docElement.Type == DocumentElementType.List ||
                docElement.Type == DocumentElementType.Table)
            {
                writer.WriteLine();
            }
            writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_LINE_MARKER);
            writer.Write(' ');
            writer.Write(docElement.NestingLevel);
            writer.Write(' ');
            writer.Write(NLPTextDocumentFormat.ElemNameForElemType[docElement.Type]);
            writer.Write(' ');
            writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_START);
            writer.Write(' ');
            switch (docElement.Type)
            {
                case DocumentElementType.Section:
                case DocumentElementType.List:
                case DocumentElementType.Table:
                    var groupElement = (GroupElementWithTitle)docElement;
                    writer.Write(groupElement.Title);
                    break;
                case DocumentElementType.TableHeader:
                case DocumentElementType.TableCell:
                    var tableElement = (TableElement)docElement;
                    if (tableElement.RowSpan == 1 && tableElement.ColSpan == 1)
                    {
                        writer.Write(tableElement.Row);
                        writer.Write(',');
                        writer.Write(tableElement.Col);
                    }
                    else
                    {
                        writer.Write(tableElement.Row);
                        writer.Write(':');
                        writer.Write(tableElement.RowSpan);
                        writer.Write(',');
                        writer.Write(tableElement.Col);
                        writer.Write(':');
                        writer.Write(tableElement.ColSpan);
                    }
                    break;
            }
            writer.WriteLine();
        }

        // ## [NestingLevel] [DocumentElementName] End
        private static void WriteDocumentElementEnd(StreamWriter writer, DocumentElement docElement)
        {
            writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_LINE_MARKER);
            writer.Write(' ');
            writer.Write(docElement.NestingLevel);
            writer.Write(' ');
            writer.Write(NLPTextDocumentFormat.ElemNameForElemType[docElement.Type]);
            writer.Write(' ');
            writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_END);
            var groupElementWithTitle = docElement as GroupElementWithTitle;
            if (groupElementWithTitle != null)
            {
                if (!String.IsNullOrEmpty(groupElementWithTitle.Title))
                {
                    writer.Write(" <<");
                    var title = groupElementWithTitle.Title;
                    if (title.Length > 47)
                    {
                        title = title.Substring(0, 47) + "...";
                    }
                    writer.Write(title);
                    writer.Write(">>");
                }
            }
            writer.WriteLine();
        }

        private static void WriteTextBlock(StreamWriter writer, string text, bool finishLine = true)
        {
            if (text.Contains("\n"))
            {
                text = text.Replace("\n", "\\n");
            }
            if (finishLine)
            {
                writer.WriteLine(text);
            }
            else
            {
                writer.Write(text);
            }
        }
    }
}
