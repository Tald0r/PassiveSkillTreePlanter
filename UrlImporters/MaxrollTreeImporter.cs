using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PassiveSkillTreePlanter.UrlDecoders;

namespace PassiveSkillTreePlanter.UrlImporters;

public class MaxrollTreeImporter : BaseUrlImporter
{
    protected override async Task<List<FetchedTree>> FetchTrees(string url, CancellationToken cancellationToken)
    {
        var nodeList = await MaxRollUrlDecoder.FetchNodeList(url, cancellationToken);
        return nodeList.TreeCollection.Variants.Select(x =>
        {
            var points = new HashSet<ushort>();
            foreach (var token in x.History)
            {
                switch (token)
                {
                    case JValue { Value: long i }:
                        points.Add((ushort)i);
                        break;
                    case JObject jo:
                        if (jo.Property("remove")?.Value is JArray removeArray)
                        {
                            foreach (var removeToken in removeArray)
                            {
                                if (removeToken is JValue { Value: long i })
                                {
                                    points.Remove((ushort)i);
                                }
                            }
                        }
                        break;
                }
            }
            return new FetchedTree(
                points.ToList(),
                x.Name,
                nodeList.Url,
                nodeList.TreeCollection.Type == "atlas" ? ESkillTreeType.Atlas : ESkillTreeType.Character,
                true);
        }).ToList();
    }

    protected override bool IsValidUrl(string url)
    {
        return MaxRollUrlDecoder.IsValidUrl(url);
    }

    protected override string Name => "Maxroll";
}