using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// https://dumps.wikimedia.org/frwiki/
// https://dumps.wikimedia.org/frwiki/20180601/frwiki-20180601-pages-articles-multistream.xml.bz2
// bzip2 -d frwiki-20180601-pages-articles-multistream.xml.bz2

// https://meta.wikimedia.org/wiki/Data_dumps
// https://www.mediawiki.org/wiki/Help:Export#Export_format
// https://www.mediawiki.org/xml/export-0.10.xsd

// <sitename>Wikipédia</sitename>
// <dbname>frwiki</dbname>
// <base>https://fr.wikipedia.org/wiki/Wikip%C3%A9dia:Accueil_principal</base>

// <page>
// <title>...</title>
//      <timestamp>2018-03-31T11:44:55Z</timestamp>
//      <username>Gaétan Lui Même</username>
// <model>wikitext</model>
// <format>text/x-wiki</format>
// <text xml:space="preserve">...
// ...
// ...</text>
// </page>

// https://en.wikipedia.org/wiki/Help:Wikitext
// https://en.wikipedia.org/wiki/Help:Cheatsheet
// https://www.mediawiki.org/wiki/Alternative_parsers
// https://github.com/sweble/sweble-wikitext
// https://codeplexarchive.blob.core.windows.net/archive/projects/WikiPlex/WikiPlex.zip

// http://mattmahoney.net/dc/textdata.html
// # Program to filter Wikipedia XML dumps to "clean" text consisting only of lowercase
//# letters (a-z, converted from A-Z), and spaces (never consecutive).  
//# All other characters are converted to spaces.  Only text which normally appears 
//# in the web browser is displayed.  Tables are removed.  Image captions are 
//# preserved.  Links are converted to normal text.  Digits are spelled out.

//# Written by Matt Mahoney, June 10, 2006.  This program is released to the public domain.

//$/=">";                     # input record separator
//while (<>) {
//  if (/<text /) {$text=1;}  # remove all but between <text> ... </text>
//  if (/#redirect/i) {$text=0;}  # remove #REDIRECT
//  if ($text) {

//    # Remove any text not normally visible
//    if (/<\/text>/) {$text=0;}
//    s/<.*>//;               # remove xml tags
//    s/&amp;/&/g;            # decode URL encoded chars
//    s/&lt;/</g;
//    s/&gt;/>/g;
//    s/<ref[^<]*<\/ref>//g;  # remove references <ref...> ... </ref>
//    s/<[^>]*>//g;           # remove xhtml tags
//    s/\[http:[^] ]*/[/g;    # remove normal url, preserve visible text
//    s/\|thumb//ig;          # remove images links, preserve caption
//    s/\|left//ig;
//    s/\|right//ig;
//    s/\|\d+px//ig;
//    s/\[\[image:[^\[\]]*\|//ig;
//    s/\[\[category:([^|\]]*)[^]]*\]\]/[[$1]]/ig;  # show categories without markup
//    s/\[\[[a-z\-]*:[^\]]*\]\]//g;  # remove links to other languages
//    s/\[\[[^\|\]]*\|/[[/g;  # remove wiki url, preserve visible text
//    s/{{[^}]*}}//g;         # remove {{icons}} and {tables}
//    s/{[^}]*}//g;
//    s/\[//g;                # remove [ and ]
//    s/\]//g;
//s/&[^;]*;/ /g;          # remove URL encoded chars

//    # convert to lowercase letters and spaces, spell digits
//    $_=" $_ ";
//    tr/A-Z/a-z/;
//    s/0/ zero /g;
//    s/1/ one /g;
//    s/2/ two /g;
//    s/3/ three /g;
//    s/4/ four /g;
//    s/5/ five /g;
//    s/6/ six /g;
//    s/7/ seven /g;
//    s/8/ eight /g;
//    s/9/ nine /g;
//    tr/a-z/ /cs;
//    chop;
//    print $_;
//  }
//}

namespace nlptextdoc.extract.wikipedia
{
    /*class WikipediaTextExtractor : IDisposable
        {
            enum DocumentSection
            {
                Section,
                List,
                Table,
                TableLine,
                TableCell,
                TextBlock,
                TextSpan,
                CodeSnippet,
                MathFormula,
                MusicScore,
                Comment
            }

            private class ParserState
            {
                public DocumentSection Section { get; set; }
                public string ClosingTag { get; set; }
            }

            // XML metadata / text
            static readonly string PAGETEXT_START = "<text";
            static readonly string PAGETEXT_END = "</text>";

            // Beginning of text
            static readonly string REDIRECT_MARKER = "#REDIRECT";

            // Beginning of line
            static readonly char SECTION_CHAR = '=';

            static readonly char INDENTATION_CHAR = ':';
            static readonly char PRESERVE_FORMATTING_CHAR = ' ';

            static readonly char BULLETED_LIST_CHAR = '*';
            static readonly char NUMBERED_LIST_CHAR = '#';
            static readonly char TERM_LIST_CHAR = ';';
            static readonly char TERM_LIST_ITEM_CHAR = ':';

            // Inside line 
            static readonly char[] MARKERS_START = new char[] { '<', '&', '[', '{', '\'', '-', '~', '_', '!', '|' };

            // Parser methods

            private FileInfo inputFile;
            private StreamReader inputReader;
            private NormalizedTextDocumentBuilder outputWriter;

            public WikipediaDumpConverter(string wikipediaXmlDumpPath)
            {
                inputFile = new FileInfo(wikipediaXmlDumpPath);
                inputReader = new StreamReader(wikipediaXmlDumpPath, Encoding.UTF8);
            }

            public void ParseToNormalizedDocument(NormalizedTextDocumentBuilder normalizedDocumentWriter)
            {
                outputWriter = normalizedDocumentWriter;

                // Read document properties
                string line = null;
                string websiteName = null;
                string websiteUrl = null;
                string databaseName = null;
                while ((line = inputReader.ReadLine()) != null)
                {
                    if (TryReadTagValue(line, "sitename", ref websiteName)) continue;
                    if (TryReadTagValue(line, "base", ref websiteUrl)) continue;
                    if (TryReadTagValue(line, "dbname", ref databaseName)) continue;
                    if (line.IndexOf("<page>") > 0) break;
                }

                string language = null;
                if (databaseName != null && databaseName.Length > 2)
                {
                    language = databaseName.Substring(0, 2).ToUpper();
                }
                string timestamp = null;
                string dumpFileName = inputFile.Name;
                int startIndexDate = dumpFileName.IndexOf('-');
                if (startIndexDate > 0)
                {
                    int endIndexDate = dumpFileName.IndexOf('-', startIndexDate + 1);
                    if (endIndexDate == (startIndexDate + 9))
                    {
                        timestamp = dumpFileName.Substring(startIndexDate + 1, 4) + "-" + dumpFileName.Substring(startIndexDate + 5, 2) + "-" + dumpFileName.Substring(startIndexDate + 7, 2);
                    }
                }

                // Write document properties
                outputWriter.WriteDocumentStart();
                if (!String.IsNullOrEmpty(language))
                {
                    outputWriter.WriteDocumentLanguage(language);
                }
                if (!String.IsNullOrEmpty(websiteName))
                {
                    outputWriter.WriteDocumentName(websiteName);
                }
                if (!String.IsNullOrEmpty(timestamp))
                {
                    outputWriter.WriteDocumentTimestamp(timestamp);
                }
                if (!String.IsNullOrEmpty(websiteUrl))
                {
                    outputWriter.WriteDocumentTitle(websiteUrl);
                }

                // Read pages properties
                while (line != null)
                {
                    string pageTitle = null;
                    string pageAuthor = null;
                    string pageTimestamp = null;
                    int textTagIndex = -1;
                    while ((line = inputReader.ReadLine()) != null)
                    {
                        if (TryReadTagValue(line, "title", ref pageTitle)) continue;
                        if (TryReadTagValue(line, "username", ref pageAuthor)) continue;
                        if (TryReadTagValue(line, "timestamp", ref pageTimestamp)) continue;
                        textTagIndex = line.IndexOf(PAGETEXT_START);
                        if (textTagIndex > 0) break;
                    }

                    // Read beginning of text - ignore REDIRECT
                    int textTagEnd = line.IndexOf(">", textTagIndex + 1);
                    string firstLineOfText = line.Substring(textTagEnd + 1);
                    if (!firstLineOfText.StartsWith(REDIRECT_MARKER))
                    {
                        // Write page properties
                        outputWriter.WriteFileStart();
                        if (!String.IsNullOrEmpty(pageTimestamp))
                        {
                            outputWriter.WriteFileTimestamp(pageTimestamp);
                        }
                        if (!String.IsNullOrEmpty(pageAuthor))
                        {
                            outputWriter.WriteFileAuthor(pageAuthor);
                        }
                        if (!String.IsNullOrEmpty(pageTitle))
                        {
                            outputWriter.WriteFileTitle(pageTitle);
                        }

                        // Parse page text
                        ParsePageText(firstLineOfText);

                        outputWriter.WriteFileEnd();
                    }

                    // Advance until next page
                    while ((line = inputReader.ReadLine()) != null)
                    {
                        if (line.IndexOf("<page>") > 0) break;
                    }
                }

                outputWriter.WriteDocumentEnd();
            }

            private static bool TryReadTagValue(string line, string tagName, ref string value)
            {
                int tagIndex = line.IndexOf("<" + tagName + ">");
                if (tagIndex > 0)
                {
                    int startIndex = tagIndex + tagName.Length + 2;
                    int endIndex = line.IndexOf("<", startIndex);
                    if (endIndex > 0)
                    {
                        value = line.Substring(startIndex, endIndex - startIndex);
                        return true;
                    }
                }
                return false;
            }

            private void ParsePageText(string firstLineOfText)
            {
                InitializeParserState();

                string line = firstLineOfText;
                do
                {
                    int endOfText = line.IndexOf(PAGETEXT_END);
                    if (endOfText > 0)
                    {
                        line = line.Substring(0, endOfText);
                        ParseTextLine(line);
                        break;
                    }
                    else
                    {
                        ParseTextLine(line);
                    }
                } while ((line = inputReader.ReadLine()) != null);
            }

            // Parser state machine

            private Stack<ParserState> parserStates = new Stack<ParserState>();

            private bool PreserveFormatting;
            private bool EscapeMarkers;

            private Stack<string> sectionHeaders = new Stack<string>();
            private int listLevel = 0;
            private bool insideDescriptionList = false;

            private void InitializeParserState()
            {
                parserStates.Clear();
                PreserveFormatting = false;
                EscapeMarkers = false;

                sectionHeaders.Clear();
                listLevel = 0;
                insideDescriptionList = false;
            }

            int lineCount = 0;

            private void ParseTextLine(string line)
            {
                lineCount++;
                if (lineCount == 1000) throw new Exception("stop");

                // Check markers at the beggining of line

                char firstChar = line[0];
                switch (firstChar)
                {
                }

                StringBuilder lineBuilder = new StringBuilder();




                outputWriter.WriteTextLine(line);
            }

            public void Dispose()
            {
                if (inputReader != null)
                {
                    inputReader.Dispose();
                    inputReader = null;
                }
            }
        }
    }*/
}
