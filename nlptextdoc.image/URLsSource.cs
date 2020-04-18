using System.Collections.Generic;
using System.Linq;

namespace nlptextdoc.image
{
    class URLsSource
    {
        internal static string[] Datasets = { "Assurance","Banque","Bourse","Comparateur","Crédit","Forum","Institution","Presse","SiteInfo" };

        internal static IEnumerable<string> ReadDatasetURLs(string dataset)
        {
            if(Datasets.Contains(dataset))
            {
                foreach(var line in FilesManager.ReadLinesFromEmbeddedFile("Dataset."  + dataset + "_urls.csv"))
                {
                    yield return line;
                }
            }
        }
    }
}
