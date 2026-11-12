using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PassiveSkillTreePlanter.UrlDecoders;

public class MaxRollUrlDecoder
{
    private static readonly Regex MaxRollUrlRegex = new Regex(@"^https://maxroll.gg/poe/poe(?:-atlas)?-tree/(?<groupId>[^/]+)(?:/(?<treeId>\d+))?", RegexOptions.Compiled);

    public static bool IsValidUrl(string url)
    {
        return MaxRollUrlRegex.IsMatch(url.Trim());
    }

    public static async Task<MaxRollFetchResult> FetchNodeList(string url, CancellationToken cancellationToken)
    {
        if (MaxRollUrlRegex.Match(url.Trim()) is not { Success: true } match)
        {
            throw new Exception($"url '{url}' does not match expected regex {MaxRollUrlRegex}");
        }

        var groupId = match.Groups["groupId"].Value;
        var dataUrl = $"https://planners.maxroll.gg/profiles/load/poe/{groupId}";
        var dataString = await new HttpClient().GetStringAsync(dataUrl, cancellationToken);
        var data = JsonConvert.DeserializeObject<MaxrollOuterData>(dataString);
        var innerData = JsonConvert.DeserializeObject<MaxRollDataRoot>(data.Data);
        MaxRollTreeCollection treeCollection;
        if (match.Groups["treeId"].Success)
        {
            var treeId = int.Parse(match.Groups["treeId"].ValueSpan);
            treeCollection = innerData?.Embeds?.FirstOrDefault(x => x?.Id == treeId) ?? throw new Exception($"Embed with id {treeId} was not found in the downloaded data");
        }
        else
        {
            treeCollection = innerData?.PassiveTree ?? throw new Exception("Non-embedded passive tree was not found in the downloaded data");
        }

        return new MaxRollFetchResult(url.Trim(), treeCollection);
    }
}

public class MaxrollOuterData
{
    [JsonProperty("data")]
    public string Data { get; set; }
}
public record MaxRollFetchResult(string Url, MaxRollTreeCollection TreeCollection);

public class MaxRollDataRoot
{
    public MaxRollTreeCollection[] Embeds { get; set; }

    [JsonProperty("passive_tree")]
    public MaxRollTreeCollection PassiveTree { get; set; }
}

public class MaxRollTreeCollection
{
    public string Type { get; set; }
    public string Name { get; set; }
    public MaxRollVariant[] Variants { get; set; }
    public int? Id { get; set; }
}

public class MaxRollVariant
{
    public string Name { get; set; }
    public JToken[] History { get; set; }
}