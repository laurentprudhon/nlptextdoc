using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace nlptextdoc.text.document
{
    /// <summary>
    /// Constants used to format the NLPTextDocument text files
    /// </summary>
    public static class NLPTextDocumentFormat
    {
        static NLPTextDocumentFormat()
        {
            foreach (var typeObj in Enum.GetValues(typeof(DocumentElementType)))
            {
                var type = (DocumentElementType)typeObj;
                var name = Enum.GetName(typeof(DocumentElementType), type);
                ElemNameForElemType.Add(type, name);
                ElemTypeForElemName.Add(name, type);
            }
        }

        internal static string DOCUMENT_ELEMENT_LINE_MARKER = "##";
        internal static string DOCUMENT_ELEMENT_START = "Start";
        internal static string DOCUMENT_ELEMENT_END = "End";

        internal static string DOCUMENT_ELEMENT_ITEMS = "Items";
        internal static string DOCUMENT_ELEMENT_ITEMS_START = ">>";
        internal static string DOCUMENT_ELEMENT_ITEMS_SEPARATOR = "||";

        internal static string TEXT_DOCUMENT_PROPERTY_PREFIX = DOCUMENT_ELEMENT_LINE_MARKER + " NLPTextDocument ";
        internal static string TEXT_DOCUMENT_TITLE = "Title";
        internal static string TEXT_DOCUMENT_URI = "Uri";
        internal static string TEXT_DOCUMENT_TIMESTAMP = "Timestamp";
        internal static string TEXT_DOCUMENT_METADATA = "Metadata";

        internal static Regex DOCUMENT_ELEMENT_LINE_REGEX = new Regex(
            DOCUMENT_ELEMENT_LINE_MARKER + " "
            + "(?<NestingLevel>[0-9]+)" + " "
            + "(?<ElementName>[A-Za-z]+)" + " "
            + "(?<Command>" + DOCUMENT_ELEMENT_START + "|" + DOCUMENT_ELEMENT_END + "|" + DOCUMENT_ELEMENT_ITEMS + ")" + " ?");

        internal static Dictionary<DocumentElementType, string> ElemNameForElemType = new Dictionary<DocumentElementType, string>();
        internal static Dictionary<string, DocumentElementType> ElemTypeForElemName = new Dictionary<string, DocumentElementType>();
    }
}
