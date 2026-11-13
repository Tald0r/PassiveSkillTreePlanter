using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using PassiveSkillTreePlanter.UrlDecoders;

namespace PassiveSkillTreePlanter.UrlImporters;

public class PobCodeImporter : BaseUrlImporter
{
    protected override Task<List<FetchedTree>> FetchTrees(string url, CancellationToken cancellationToken)
    {
        var xml = PobHelpers.CodeToXml(url);
        var xmlDocument = new XmlDocument();
        //because loading xml securely is surprisingly hard
        xmlDocument.Load(XmlReader.Create(new StringReader(xml)));
        var root = xmlDocument.GetElementsByTagName("PathOfBuilding").Cast<XmlNode>().First();
        var tree = root.ChildNodes.Cast<XmlNode>().First(x => x.Name == "Tree");
        var urls = tree.ChildNodes.Cast<XmlNode>()
            .Where(x => x.Name == "Spec")
            .Select(ReadSpec)
            .Where(x => x != null)
            .ToList();
        return Task.FromResult(urls);
    }

    private static FetchedTree ReadSpec(XmlNode spec)
    {
        var url = spec["URL"]?.InnerText.Trim();
        if (url == null || !PathOfExileUrlDecoder.TryMatch(url, out var type, out var passives, out var masteries))
            return null;

        var selections = spec.Attributes?["masteryEffects"]?.Value;
        if (!string.IsNullOrEmpty(selections))
        {
            foreach (Match pair in Regex.Matches(selections, @"\{(\d+),(\d+)\}"))
            {
                if (ushort.TryParse(pair.Groups[1].ValueSpan, out var node) &&
                    ushort.TryParse(pair.Groups[2].ValueSpan, out var effect) && passives.Contains(node))
                    masteries[node] = effect;
            }
        }

        return new FetchedTree(passives.ToList(), spec.Attributes?["title"]?.Value, "", type, false, masteries);
    }

    protected override bool IsValidUrl(string url)
    {
        return Convert.TryFromBase64String(url.Replace('-', '+').Replace('_', '/'), new byte[url.Length], out _);
    }

    protected override string Name => "PoB";

    protected override uint UrlMaxLength => 100000;
}
