using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using nlptextdoc.text.document;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace nlptextdoc.extract.html
{
    /// <summary>
    /// Converts an Html syntax tree as parsed by Anglesharp
    /// to a simplified NLPTextDocument
    /// </summary>
    public class HtmlDocumentConverter
    {
        /// <summary>
        /// source Uri of the Html document
        /// </summary>
        private string absoluteUri;

        /// <summary>
        /// Html document to converts
        /// </summary>
        private IDocument htmlDocument;

        /// <summary>
        /// Resulting NLPTextDocument
        /// </summary>
        private NLPTextDocument textDocument;

        public HtmlDocumentConverter(string absoluteUri, IDocument htmlDocument)
        {
            this.absoluteUri = absoluteUri;
            this.htmlDocument = htmlDocument;
        }

        /// <summary>
        /// Start of the tree traversal at the document level
        /// </summary>
        public NLPTextDocument ConvertToNLPTextDocument()
        {
            if (textDocument == null)
            {
                docBuilder = new NLPTextDocumentBuilder(absoluteUri);
                textBuilderStack = new Stack<StringBuilder>();
                tableCoordsStack = new Stack<TableCoords>();
                if (htmlDocument.HasChildNodes)
                {
                    // Analyse document structure to find where to attach the section headers 
                    AnalyseDocumentStructureToDelimitSections();
                    // Traverse the tree of the Html document
                    VisitChildNodes(htmlDocument);
                }
                textDocument = docBuilder.TextDocument;
                tableCoordsStack = null;
                textBuilderStack = null;
                docBuilder = null;
            }
            return textDocument;
        }

        /// <summary>
        /// Analyse the document structure to find where to attach the section headers 
        /// </summary>
        private void AnalyseDocumentStructureToDelimitSections()
        {
            // 1. Find all section headers
            var sectionHeaders = htmlDocument.All.Where(m => m.LocalName.StartsWith("h") &&
                (m.LocalName == "h1" || m.LocalName == "h2" || m.LocalName == "h3" ||
                 m.LocalName == "h4" || m.LocalName == "h5" || m.LocalName == "h6")).ToList();
            // 2. List and store for later use the parent elements of all section headers
            var headersParentElements = new Dictionary<IElement, List<IElement>>();
            foreach (var header in sectionHeaders)
            {
                var parentElements = new List<IElement>();
                var node = header;
                parentElements.Add(node);
                while ((node = node.ParentElement) != null) parentElements.Add(node);
                headersParentElements.Add(header, parentElements);
            }
            // 3. Find lowest common ancestor elements between two successive section headers
            var headersContainersList = new List<HeaderContainersCandidates>();
            var headersNestingStack = new Stack<HeadersNestingState>();
            headersNestingStack.Push(new HeadersNestingState() { RootElement = htmlDocument.FirstElementChild, PreviousHeader = null });
            foreach (var header in sectionHeaders)
            {
                // 3.1 Handle header nesting state
                var rootElement = headersNestingStack.Peek().RootElement;
                while (!headersParentElements[header].Contains(rootElement))
                {
                    headersNestingStack.Pop();
                    rootElement = headersNestingStack.Peek().RootElement;
                }
                var previousHeader = headersNestingStack.Peek().PreviousHeader;
                if (previousHeader != null && headersParentElements[header].Contains(previousHeader))
                {
                    rootElement = previousHeader;
                    previousHeader = null;
                    headersNestingStack.Push(new HeadersNestingState() { RootElement = rootElement, PreviousHeader = previousHeader });
                }
                // 3.2 Store header candidate containers
                var headerContainers = new HeaderContainersCandidates() { Header = header, RootElement = rootElement };
                headersContainersList.Add(headerContainers);
                if (previousHeader != null)
                {
                    // Find common ancestor with previous header
                    var parentsOfPreviousHeader = headersParentElements[previousHeader];
                    var parentsOfCurrentHeader = headersParentElements[header];
                    foreach (var parentOfCurrentHeader in parentsOfCurrentHeader)
                    {
                        int indexOfAncestorInPrevious = parentsOfPreviousHeader.IndexOf(parentOfCurrentHeader);
                        if (indexOfAncestorInPrevious > 0)
                        {
                            headerContainers.CommonParentWithPrevious = parentOfCurrentHeader;
                            int indexOfAncestorInCurrent = parentsOfCurrentHeader.IndexOf(parentOfCurrentHeader);
                            headerContainers.ContainerIfGroupedWithPrevious = parentsOfCurrentHeader[indexOfAncestorInCurrent - 1];

                            var previousHeaderContainers = headersContainersList[headersContainersList.Count - 2];
                            previousHeaderContainers.CommonParentWithNext = parentOfCurrentHeader;
                            previousHeaderContainers.ContainerIfGroupedWithNext = parentsOfPreviousHeader[indexOfAncestorInPrevious - 1];

                            break;
                        }
                    }
                }
                // Pass current header to next iteration
                headersNestingStack.Peek().PreviousHeader = header;
            }
            // 4. Select and store header containers
            sectionHeadersForContainerElements = new Dictionary<INode, IElement>();
            for (int i = 0; i < sectionHeaders.Count; i++)
            {
                var header = sectionHeaders[i];
                var headerParents = headersParentElements[header];
                var headerCandidateContainers = headersContainersList[i];
                // Select the deepest available container
                int distanceWithPrevious = (headerCandidateContainers.CommonParentWithPrevious == null) ? Int32.MaxValue :
                    headerParents.IndexOf(headerCandidateContainers.CommonParentWithPrevious);
                int distanceWithNext = (headerCandidateContainers.CommonParentWithNext == null) ? Int32.MaxValue :
                    headerParents.IndexOf(headerCandidateContainers.CommonParentWithNext);
                IElement selectedContainer = null;
                // Different depths
                if (distanceWithPrevious != distanceWithNext)
                {
                    selectedContainer = (distanceWithPrevious < distanceWithNext) ?
                        headerCandidateContainers.ContainerIfGroupedWithPrevious :
                        headerCandidateContainers.ContainerIfGroupedWithNext;
                }
                // Same non null depth
                else if (distanceWithPrevious != Int32.MaxValue)
                {
                    if (headerCandidateContainers.ContainerIfGroupedWithPrevious is IHtmlHeadElement)
                    {
                        selectedContainer = headerCandidateContainers.ContainerIfGroupedWithNext;
                    }
                    else
                    {
                        selectedContainer = headerCandidateContainers.ContainerIfGroupedWithPrevious;
                    }
                }
                // Register an action only if container element distinct of header element
                if (selectedContainer != null && selectedContainer != header &&
                    selectedContainer.FirstElementChild != header)
                {
                    sectionHeadersForContainerElements[selectedContainer] = header;
                }
            }
        }

        class HeadersNestingState
        {
            public IElement RootElement;
            public IElement PreviousHeader;
        }

        class HeaderContainersCandidates
        {
            public IElement Header;

            public IElement RootElement;

            public IElement CommonParentWithPrevious;
            public IElement ContainerIfGroupedWithPrevious;

            public IElement CommonParentWithNext;
            public IElement ContainerIfGroupedWithNext;
        }

        // >>> Parsing state 

        // Track nested DocumentElements
        private NLPTextDocumentBuilder docBuilder;

        // Track nested TableCells
        private Stack<TableCoords> tableCoordsStack;
        class TableCoords
        {
            public int Row = 1;
            public int Col = 1;
            public List<TableCoords> RowSpanCells;
            internal bool IsRowSpanCell()
            {
                if (RowSpanCells != null)
                {
                    foreach (var tableCoords in RowSpanCells)
                    {
                        if (tableCoords.Row == Row && tableCoords.Col == Col)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        // Track nested TextBlocks
        private Stack<StringBuilder> textBuilderStack;        
        private bool disableTextBlockOutput;
        
        // Track nested Sections 
        private Dictionary<INode, IElement> sectionHeadersForContainerElements;
               
        // <<< Parsing state

        /// <summary>
        /// Recursively visit a list of child nodes
        /// </summary>
        private void VisitChildNodes(INode parentNode)
        {
            // Start section if a header is attached to this node
            Stack<IElement> headersNestedAtThisLevel = null;
            if (sectionHeadersForContainerElements.ContainsKey(parentNode))
            {
                var headerForThisContainer = sectionHeadersForContainerElements[parentNode];
                VisitHeaderAndStartSection(headerForThisContainer, ref headersNestedAtThisLevel);
            }
            // Traverse all children nodes
            // 
            foreach (var htmlNode in parentNode.ChildNodes)
            {
                switch (htmlNode.NodeType)
                {
                    case NodeType.Element:
                        var htmlElement = (IElement)htmlNode;
                        // Quick exit for invisible elements based on their class name
                        // (we don't evaluate Css properties here for performance reasons)
                        if (FilterElementBasedOnAttributes(htmlElement))
                        {
                            continue;
                        }
                        // Handle eahc child node based on its Html tag type
                        string elementName = htmlElement.TagName.ToLower();
                        switch (elementName)
                        {
                            case "script":
                            case "noscript":
                            case "style":
                            case "svg":
                                continue;
                            case "title":
                                VisitTitle(htmlElement);
                                break;
                            case "html":
                                VisitHtml(htmlElement);
                                break;
                            case "h1":
                            case "h2":
                            case "h3":
                            case "h4":
                            case "h5":
                            case "h6":
                                // Ignore headers if they are attached to a parent node higher up in the syntax tree
                                if (!sectionHeadersForContainerElements.Values.Contains(htmlElement))
                                {
                                    VisitHeaderAndStartSection(htmlElement, ref headersNestedAtThisLevel);
                                }
                                break;
                            case "ul":
                            case "ol":
                                VisitList(htmlElement);
                                break;
                            case "li":
                                VisitListItem(htmlElement);
                                break;
                            case "table":
                                VisitTable(htmlElement);
                                break;
                            case "tr":
                                VisitTableRow(htmlElement);
                                break;
                            case "th":
                            case "td":
                                VisitTableHeaderOrCell(htmlElement);
                                break;
                            case "img":
                                VisitImage(htmlElement);
                                break;
                            case "a":
                                VisitLink(htmlElement);
                                break;
                            default:
                                VisitOtherHtmlElement(htmlElement);
                                break;
                        }
                        break;
                    case NodeType.Text:
                        VisitTextNode(htmlNode);
                        break;
                }
            }
            // Manage the nesting of section elements in each container
            if (headersNestedAtThisLevel != null)
            {
                while (headersNestedAtThisLevel.Count > 0)
                {
                    headersNestedAtThisLevel.Pop();
                    EndSection();
                }
            }
        }

        // Try to detect invisible elements based on their class name
        // (we don't evaluate Css properties here for performance reasons)
        private bool FilterElementBasedOnAttributes(IElement htmlElement)
        {
            string classAttr = htmlElement.Attributes["class"]?.Value;
            string roleAttr = htmlElement.Attributes["role"]?.Value;
            string ariaHiddenAttr = htmlElement.Attributes["aria-hidden"]?.Value;
            if ((classAttr != null && (classAttr.Contains("hidden") || classAttr.Contains("invisible") ||
                                       classAttr.Contains("login") || classAttr.Contains("search"))) ||
                (roleAttr != null && roleAttr.Contains("search")) ||
                 ariaHiddenAttr != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // ----------------------------------------
        // Methods to analyse each type of Html tag

        private void VisitTextNode(INode htmlNode)
        {
            var htmlTextNode = (IText)htmlNode;
            string text = htmlTextNode.Text.Trim();
            if (!String.IsNullOrEmpty(text))
            {
                AppendText(text);
            }
        }       

        private void VisitLink(IElement htmlElement)
        {
            if (!htmlElement.ChildNodes.Any() && htmlElement.HasAttribute("title"))
            {
                AppendText(htmlElement.GetAttribute("title"));
            }
            else
            {
                VisitOtherHtmlElement(htmlElement);
            }
        }
        
        private void VisitHtml(IElement htmlElement)
        {
            OnStartOfTextBlock();
            if (htmlElement.HasChildNodes)
            {
                VisitChildNodes(htmlElement);
            }
            OnEndOfTextBlock();
        }

        private void VisitOtherHtmlElement(IElement htmlElement)
        {
            bool collectTextBlockOutput = !disableTextBlockOutput && IsBlockElement(htmlElement);
            if(collectTextBlockOutput)
            {
                OnStartOfTextBlock();
            }
            if (htmlElement.HasChildNodes)
            {
                VisitChildNodes(htmlElement);
            }
            if (collectTextBlockOutput)
            {
                OnEndOfTextBlock();
            }
        }

        private void VisitTitle(IElement htmlElement)
        {
            string title = null;
            if (htmlElement.HasChildNodes)
            {
                OnStartOfTextBlock(true);
                VisitChildNodes(htmlElement);
                title = OnEndOfTextBlock(true);
            }
            docBuilder.SetTitle(title);
        }

        private void VisitHeaderAndStartSection(IElement headerElement, ref Stack<IElement> headersNestedAtThisLevel)
        {
            if (headersNestedAtThisLevel == null) headersNestedAtThisLevel = new Stack<IElement>();
            // Pop from the headers stacked at the current level
            // all the headers which have a html level (h1 => 1, h6 => 6) 
            // lower or equal to the one of the current header
            int currentHtmlHeaderLevel = GetHtmlHeaderLevel(headerElement);
            while (headersNestedAtThisLevel.Count > 0)
            {
                if (currentHtmlHeaderLevel <= GetHtmlHeaderLevel(headersNestedAtThisLevel.Peek()))
                {
                    EndSection();
                    headersNestedAtThisLevel.Pop();
                }
                else
                {
                    break;
                }
            }
            // Traverse chid nodes of the h1 ... h6 html header
            // to gather the text of the title of the new section
            // then create the new section
            string title = null;
            if (headerElement.HasChildNodes)
            {
                OnStartOfTextBlock(true);
                VisitChildNodes(headerElement);
                title = OnEndOfTextBlock(true);
            }
            docBuilder.StartSection(title);
            // Push the current header on the stack 
            // of the headers nested at this level
            headersNestedAtThisLevel.Push(headerElement);
        }

        private static int GetHtmlHeaderLevel(IElement headerElement)
        {
            return Int32.Parse(headerElement.TagName.Substring(1));
        }

        private void EndSection()
        {
            docBuilder.EndSection();
        }

        private void VisitList(IElement htmlElement)
        {
            bool isNavigationList = DetectNavigationList(htmlElement);
            if (isNavigationList)
            {
                docBuilder.StartNavigationList();
            }
            else
            {
                docBuilder.StartList();
            }
            if (htmlElement.HasChildNodes)
            {
                VisitChildNodes(htmlElement);
            }
            docBuilder.EndList();
        }

        // Handle navigation menus as a specific case, as we may want to
        // filter them out when we work on the text extracted from a web page
        // => if a list contains only Html links as list items (except at most one)
        private bool DetectNavigationList(IElement listElement)
        {
            int anchorElementCount = 0;
            int nonAnchorElementCount = 0;
            foreach(var listItemElement in listElement.Children)
            {
                if (!(listItemElement is IHtmlListItemElement)) return false;
                foreach (var listItemContentElement in listItemElement.Children)
                {
                    if (listItemContentElement is IHtmlAnchorElement)
                    {
                        anchorElementCount++;
                    }
                    else
                    {
                        nonAnchorElementCount++;
                    }
                    if (nonAnchorElementCount > 1) return false;
                }
            }
            if (anchorElementCount > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void VisitListItem(IElement htmlElement)
        {
            docBuilder.StartListItem();
            OnStartOfTextBlock();
            if (htmlElement.HasChildNodes)
            {
                VisitChildNodes(htmlElement);
            }
            OnEndOfTextBlock();
            docBuilder.EndListItem();
        }

        private void VisitTable(IElement htmlElement)
        {
            docBuilder.StartTable();
            tableCoordsStack.Push(new TableCoords());
            if (htmlElement.HasChildNodes)
            {
                VisitChildNodes(htmlElement);
            }
            tableCoordsStack.Pop();
            docBuilder.EndTable();
        }

        private void VisitTableRow(IElement htmlElement)
        {
            if (htmlElement.HasChildNodes)
            {
                VisitChildNodes(htmlElement);
            }
            var tableCoords = tableCoordsStack.Peek();
            tableCoords.Row++;
            tableCoords.Col = 1;
        }

        private void VisitTableHeaderOrCell(IElement htmlElement)
        {
            OnStartOfTextBlock();
            var tableCoords = tableCoordsStack.Peek();
            while (tableCoords.IsRowSpanCell())
            {
                tableCoords.Col++;
            }
            int rowSpan = 1;
            var rowSpanAttribute = htmlElement.Attributes["rowspan"];
            if (rowSpanAttribute != null)
            {
                Int32.TryParse(rowSpanAttribute.Value, out rowSpan);
            }
            int colSpan = 1;
            var colSpanAttribute = htmlElement.Attributes["colspan"];
            if (colSpanAttribute != null)
            {
                Int32.TryParse(colSpanAttribute.Value, out colSpan);
            }
            var htmlElementName = htmlElement.TagName.ToLower();
            if (htmlElementName == "th")
            {
                docBuilder.StartTableHeader(tableCoords.Row, tableCoords.Col, rowSpan, colSpan);
            }
            else
            {
                docBuilder.StartTableCell(tableCoords.Row, tableCoords.Col, rowSpan, colSpan);
            }
            if (htmlElement.HasChildNodes)
            {
                VisitChildNodes(htmlElement);
            }
            OnEndOfTextBlock();
            if (htmlElementName == "th")
            {
                docBuilder.EndTableHeader();
            }
            else
            {
                docBuilder.EndTableCell();
            }
            if (rowSpan > 1)
            {
                if (tableCoords.RowSpanCells == null)
                {
                    tableCoords.RowSpanCells = new List<TableCoords>();
                }
                for (int addRow = 1; addRow < rowSpan; addRow++)
                {
                    for (int addCol = 0; addCol < colSpan; addCol++)
                    {
                        tableCoords.RowSpanCells.Add(new TableCoords() { Row = tableCoords.Row + addRow, Col = tableCoords.Col + addCol });
                    }
                }
            }
            tableCoords.Col += colSpan;
        }

        private void VisitImage(IElement htmlElement)
        {
            IHtmlImageElement imageElement = htmlElement as IHtmlImageElement;
            if (imageElement != null && imageElement.HasAttribute("alt"))
            {
                var alt = imageElement.GetAttribute("alt");
                if (!String.IsNullOrEmpty(alt))
                {
                    AppendText(alt);   
                }
            }
        }

        // --- Collect text grouped in text blocks ---

        // Gradually collect text for a given TextBlock while
        // while visiting many Html nodes
        private void AppendText(string text)
        {
            if (textBuilderStack.Count > 0)
            {
                StringBuilder textBuilder = textBuilderStack.Peek();
                if (textBuilder.Length > 0)
                {
                    textBuilder.Append(' ');
                }

                text = WebUtility.HtmlDecode(text.Trim()).Trim('\u00A0');
                textBuilder.Append(text);
            }
        }

        private void OnStartOfTextBlock(bool collectPropertyText = false)
        {
            textBuilderStack.Push(new StringBuilder());
            if (collectPropertyText) disableTextBlockOutput = true;
        }

        private string OnEndOfTextBlock(bool collectPropertyText = false)
        {
            if (collectPropertyText) disableTextBlockOutput = false;
            var textBuilder = textBuilderStack.Pop();

            string text = null;
            if (textBuilder.Length > 0)
            {
                text = textBuilder.ToString();
                if(!collectPropertyText) docBuilder.AddTextBlock(text);
            }
            return text;
        }

        // We group TextBlocks per Html block-level element
        // IMPORTANT : this method is very very expensive performance-wise
        // because it needs to evaluate Css properties applied to <a> and <span>
        // Html tags, which are inline elements often promoted to block-level elements
        // by Css stylesheets
        private bool IsBlockElement(IElement htmlElement)
        {
            var htmlElementName = htmlElement.TagName.ToLower();
            switch (htmlElementName)
            {
                case "article":
                case "aside":
                case "blockquote":
                case "body":
                case "br":
                case "button":
                case "canvas":
                case "caption":
                case "col":
                case "colgroup":
                case "dd":
                case "div":
                case "dl":
                case "dt":
                case "embed":
                case "fieldset":
                case "figcaption":
                case "figure":
                case "footer":
                case "form":
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                case "header":
                case "hgroup":
                case "hr":
                case "li":
                case "map":
                case "object":
                case "ol":
                case "output":
                case "p":
                case "pre":
                case "progress":
                case "section":
                case "table":
                case "tbody":
                case "textarea":
                case "tfoot":
                case "th":
                case "thead":
                case "tr":
                case "ul":
                case "video":
                    return true;
                case "a":
                case "span":                    
                    var cssStyle = htmlDocument.DefaultView.GetComputedStyleInReadOnlyDocument(htmlElement);
                    var display = cssStyle.Display;
                    if (!String.IsNullOrEmpty(display) && display.EndsWith("block"))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                default:
                    return false;
            }
        }
    }    
}
