using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace nlptextdoc.image2
{
    class JavascriptInterop
    {
        static string JS_FILE_PATH = "extracttext.js";
        static string JS_COMMAND = "extractText({0})";

        static string javascriptDefinitions;

        static JavascriptInterop()
        {
           javascriptDefinitions = FilesManager.ReadStringFromEmbeddedFile(JS_FILE_PATH);
        }

        internal async static Task InjectJavascriptDefinitionsAsync(CoreWebView2 webview)
        {
            await webview.ExecuteScriptAsync(javascriptDefinitions);
            await webview.ExecuteScriptAsync("document.body.style.overflow = 'hidden';");
        }

        internal async static Task<string> ExecuteJavascriptCodeAsync(CoreWebView2 webview, string javascriptCode)
        {
            var result = await webview.ExecuteScriptAsync(javascriptCode);
            if (result.Length > 0 && result.StartsWith('"'))
            {
                return result.Substring(1, result.Length - 2);
            } 
            else
            {
                return result;
            }
        }

        internal async static Task<string> ExtractTextAsJson(CoreWebView2 webview, bool displayColoredRectangles)
        {
            var extractTextCommand = String.Format(JS_COMMAND, displayColoredRectangles.ToString().ToLower());
            var jsonResult = await ExecuteJavascriptCodeAsync(webview, extractTextCommand);
            return jsonResult;
        }

        internal async static Task<PageElement> ExtractTextAsPageElementsAsync(CoreWebView2 webview, bool displayColoredRectangles)
        {
            var jsonResult = await ExtractTextAsJson(webview, displayColoredRectangles);
            var pageTree = ConvertJsonToPageElements(jsonResult);
            return pageTree;
        }

        internal static PageElement ConvertJsonToPageElements(string jsonResult)
        {
            return JsonConvert.DeserializeObject<PageElement>(jsonResult);
        }

        internal static async Task<string> GetUniqueFileNameFromURLAsync(CoreWebView2 webview)
        {
            var url = await JavascriptInterop.ExecuteJavascriptCodeAsync(webview, "document.location.href");
            var fileName = HtmlFileUtils.GetFileNameFromUri(url);
            return fileName;
        }
    }
}
