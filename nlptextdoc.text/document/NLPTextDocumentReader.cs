using System;
using System.IO;
using System.Text;

namespace nlptextdoc.text.document
{
    /// <summary>
    /// Read a NLPTextDocument from a text file on disk 
    /// </summary>
    public static class NLPTextDocumentReader
    {
        public static NLPTextDocument ReadFromFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new ArgumentException($"File not found at path {path}");
            }
            using (var reader = new StreamReader(path, Encoding.UTF8))
            {
                var builder = new NLPTextDocumentBuilder();
                string line = null;
                bool isReadingDocumentProperties = true;
                while ((line = reader.ReadLine()) != null)
                {
                    if (String.IsNullOrEmpty(line)) continue;

                    if (isReadingDocumentProperties)
                    {
                        if (line.StartsWith(NLPTextDocumentFormat.TEXT_DOCUMENT_PROPERTY_PREFIX))
                        {
                            ReadTextDocumentProperty(line.Substring(NLPTextDocumentFormat.TEXT_DOCUMENT_PROPERTY_PREFIX.Length), builder);
                        }
                        else
                        {
                            isReadingDocumentProperties = false;
                        }
                    }
                    if (!isReadingDocumentProperties)
                    {
                        ReadDocumentElement(line, builder);
                    }
                }
                return builder.TextDocument;
            }
        }

        private static void ReadTextDocumentProperty(string propertyAndValue, NLPTextDocumentBuilder builder)
        {
            int firstSpaceIndex = propertyAndValue.IndexOf(' ');
            if (firstSpaceIndex < 0)
            {
                throw new Exception("Invalid file format");
            }
            var propertyValue = propertyAndValue.Substring(firstSpaceIndex + 1).Trim();

            if (propertyAndValue.StartsWith(NLPTextDocumentFormat.TEXT_DOCUMENT_TITLE))
            {
                builder.SetTitle(propertyValue);
            }
            else if (propertyAndValue.StartsWith(NLPTextDocumentFormat.TEXT_DOCUMENT_URI))
            {
                builder.SetUri(propertyValue);
            }
            else if (propertyAndValue.StartsWith(NLPTextDocumentFormat.TEXT_DOCUMENT_TIMESTAMP))
            {
                builder.SetTimestamp(propertyValue);
            }
            else if (propertyAndValue.StartsWith(NLPTextDocumentFormat.TEXT_DOCUMENT_METADATA))
            {
                int firstEqualsIndex = propertyValue.IndexOf('=');
                if (firstEqualsIndex < 0)
                {
                    throw new Exception("Invalid file format");
                }
                var key = propertyValue.Substring(0, firstEqualsIndex).Trim();
                var value = propertyValue.Substring(firstEqualsIndex + 1).Trim();
                builder.AddMetadata(key, value);
            }
            else
            {
                throw new Exception("Invalid file format");
            }
        }

        private static void ReadDocumentElement(string line, NLPTextDocumentBuilder builder)
        {
            if (line.StartsWith(NLPTextDocumentFormat.DOCUMENT_ELEMENT_LINE_MARKER))
            {
                var match = NLPTextDocumentFormat.DOCUMENT_ELEMENT_LINE_REGEX.Match(line);
                if (match.Success)
                {
                    try
                    {
                        var vestingLevel = Int32.Parse(match.Groups["NestingLevel"].Value);
                        var elementName = match.Groups["ElementName"].Value;
                        var elementType = NLPTextDocumentFormat.ElemTypeForElemName[elementName];
                        var command = match.Groups["Command"].Value;

                        if (command == NLPTextDocumentFormat.DOCUMENT_ELEMENT_START)
                        {
                            string title = null;
                            int row = 1, rowspan = 1;
                            int col = 1, colspan = 1;
                            if (elementType == DocumentElementType.Section ||
                                elementType == DocumentElementType.NavigationList ||
                                elementType == DocumentElementType.List ||
                                elementType == DocumentElementType.Table)
                            {
                                title = line.Substring(match.Length).Trim();
                                if (title.Length == 0) title = null;
                            }
                            else if (elementType == DocumentElementType.TableHeader ||
                                    elementType == DocumentElementType.TableCell)
                            {
                                var values = line.Substring(match.Length).Trim();
                                var coords = values.Split(',', ':');
                                if (coords.Length == 2)
                                {
                                    row = Int32.Parse(coords[0]);
                                    col = Int32.Parse(coords[1]);
                                }
                                else if (coords.Length == 4)
                                {
                                    row = Int32.Parse(coords[0]);
                                    rowspan = Int32.Parse(coords[1]);
                                    col = Int32.Parse(coords[2]);
                                    colspan = Int32.Parse(coords[3]);
                                }
                            }

                            switch (elementType)
                            {
                                case DocumentElementType.Section:
                                    builder.StartSection(title);
                                    break;
                                case DocumentElementType.NavigationList:
                                    builder.StartNavigationList(title);
                                    break;
                                case DocumentElementType.List:
                                    builder.StartList(title);
                                    break;
                                case DocumentElementType.ListItem:
                                    builder.StartListItem();
                                    break;
                                case DocumentElementType.Table:
                                    builder.StartTable(title);
                                    break;
                                case DocumentElementType.TableHeader:
                                    builder.StartTableHeader(row, col, rowspan, colspan);
                                    break;
                                case DocumentElementType.TableCell:
                                    builder.StartTableCell(row, col, rowspan, colspan);
                                    break;
                            }
                        }
                        else if (command == NLPTextDocumentFormat.DOCUMENT_ELEMENT_END)
                        {
                            switch (elementType)
                            {
                                case DocumentElementType.Section:
                                    builder.EndSection();
                                    break;
                                case DocumentElementType.List:
                                case DocumentElementType.NavigationList:
                                    builder.EndList();
                                    break;
                                case DocumentElementType.ListItem:
                                    builder.EndListItem();
                                    break;
                                case DocumentElementType.Table:
                                    builder.EndTable();
                                    break;
                                case DocumentElementType.TableHeader:
                                    builder.EndTableHeader();
                                    break;
                                case DocumentElementType.TableCell:
                                    builder.EndTableCell();
                                    break;
                            }
                        }
                        else if (command == NLPTextDocumentFormat.DOCUMENT_ELEMENT_ITEMS)
                        {
                            var startOfItems = line.IndexOf(NLPTextDocumentFormat.DOCUMENT_ELEMENT_ITEMS_START);

                            var title = line.Substring(match.Length, startOfItems - match.Length).Trim();
                            if (title.Length == 0) title = null;
                            if (elementType == DocumentElementType.NavigationList)
                            {
                                builder.StartNavigationList(title);
                            }
                            else if (elementType == DocumentElementType.List)
                            {
                                builder.StartList(title);
                            }

                            var items = line.Substring(startOfItems + NLPTextDocumentFormat.DOCUMENT_ELEMENT_ITEMS_START.Length).Split(new string[] { NLPTextDocumentFormat.DOCUMENT_ELEMENT_ITEMS_SEPARATOR }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var item in items)
                            {
                                var text = item.Trim();
                                if (!String.IsNullOrEmpty(text))
                                {
                                    builder.StartListItem();
                                    builder.AddTextBlock(text);
                                    builder.EndListItem();
                                }
                            }

                            builder.EndList();
                        }
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"File format error on line : {line.Substring(0, Math.Min(line.Length, 50))}");
                    }
                }
                else
                {
                    throw new Exception($"File format error on line {line.Substring(0, Math.Min(line.Length, 50))}");
                }
            }
            else
            {
                builder.AddTextBlock(line);
            }
        }
    }
}
