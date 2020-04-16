using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace nlptextdoc.image
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

        internal async static void InjectJavascriptDefinitions(WebView webview)
        {
            await webview.InvokeScriptAsync("eval", new[] { javascriptDefinitions });
        }

        internal async static Task<string> ExecuteJavascriptCode(WebView webview, string javascriptCode)
        {
            var result = await webview.InvokeScriptAsync("eval", new[] { javascriptCode });
            return result;
        }

        internal async static Task<string> ExtractTextAsJson(WebView webview, bool displayColoredRectangles)
        {
            var extractTextCommand = String.Format(JS_COMMAND, displayColoredRectangles.ToString().ToLower());
            var jsonResult = await ExecuteJavascriptCode(webview, extractTextCommand);
            return jsonResult;
        }
        internal async static Task<PageElement> ExtractTextAsPageElements(WebView webview, bool displayColoredRectangles)
        {
            var jsonResult = await ExtractTextAsJson(webview, displayColoredRectangles);
            var pageTree = ConvertJsonToPageElements(jsonResult);
            return pageTree;
        }

        internal static PageElement ConvertJsonToPageElements(string jsonResult)
        {
            return JsonConvert.DeserializeObject<PageElement>(jsonResult);
        }
    }
}
