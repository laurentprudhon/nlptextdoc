using nlptextdoc.text.document;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

namespace nlptextdoc.extract.pdf
{
    /// <summary>
    /// Converts a PDF file parsed by PdfPig to a simplified NLPTextDocument
    /// </summary>
    public static class PdfDocumentConverter
    {
        /// <summary>
        /// Start of the tree traversal at the document level
        /// </summary>
        public static NLPTextDocument ConvertToNLPTextDocument(string absoluteUri, PdfDocument pdfDocument)
        {
            var docBuilder = new NLPTextDocumentBuilder(absoluteUri);
            docBuilder.SetTitle(pdfDocument.Information.Title);
            var metadataDict = pdfDocument.Information.DocumentInformationDictionary.Data;
            foreach (var key in metadataDict.Keys)
            {
                if(key != "Title" && metadataDict[key] is UglyToad.PdfPig.Tokens.StringToken)
                {
                    docBuilder.AddMetadata(key, ((UglyToad.PdfPig.Tokens.StringToken)metadataDict[key]).Data);
                }
            }
            for (var i = 1; i <= pdfDocument.NumberOfPages; i++)
            {
                var page = pdfDocument.GetPage(i);
                docBuilder.StartSection($"Page {i}");

                // Use default parameters
                // - within line angle is set to [-30, +30] degree (horizontal angle)
                // - between lines angle is set to [45, 135] degree (vertical angle)
                // - between line multiplier is 1.3
                var words = page.GetWords();
                var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
                foreach (var block in blocks)
                {
                    docBuilder.AddTextBlock(block.Text);    
                }

                docBuilder.EndSection();
            }
            return docBuilder.TextDocument;
        }
    }
}
