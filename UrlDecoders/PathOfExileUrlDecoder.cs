using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PassiveSkillTreePlanter.UrlDecoders;

public class PathOfExileUrlDecoder
{
    //Many thanks to https://github.com/EmmittJ/PoESkillTree
    private static readonly Regex UrlRegex =
        new Regex(@"^(http(|s):\/\/|)(\w*\.|)pathofexile\.com\/(fullscreen-|)(?<type>atlas|passive)-skill-tree\/(\d+(\.\d+)+\/)?(?<build>[\w-=]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool TryMatch(string buildUrl, out ESkillTreeType type, out HashSet<ushort> passiveIds,
        out Dictionary<ushort, ushort> masteryEffects)
    {
        masteryEffects = new();
        if (UrlRegex.Match(buildUrl) is { Success: true } match)
        {
            type = match.Groups["type"].Value switch
            {
                "atlas" => ESkillTreeType.Atlas,
                "passive" => ESkillTreeType.Character
            };
            passiveIds = Decode(match.Groups["build"].Value, masteryEffects);
            return true;
        }

        type = default;
        passiveIds = default;
        return false;
    }

    private static HashSet<ushort> Decode(string buildCode, Dictionary<ushort, ushort> masteryEffects)
    {
        var nodeIds = new HashSet<ushort>();

        var textToDecode = buildCode.Replace('-', '+').Replace('_', '/');

        var data = Convert.FromBase64String(textToDecode.PadRight((textToDecode.Length + 3) & ~3, '='));

        var version = BinaryPrimitives.ReadInt32BigEndian(data);
        var start = version >= 4 ? 7 : 6;
        var end = version >= 5 ? start + 2 * data[6] : data.Length;

        for (var k = start; k < end; k += 2)
        {
            nodeIds.Add(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(k, 2)));
        }

        // Version 6 stores cluster nodes separately, followed by (effect ID, mastery node ID) pairs.
        // Cluster IDs are relative to 65536 and cannot address nodes on the base tree.
        if (version == 6 && end < data.Length)
        {
            var offset = end + 1 + 2 * data[end];
            if (offset < data.Length)
            {
                var count = data[offset++];
                var entries = data.AsSpan(offset, count * 4);
                for (var i = 0; i < entries.Length; i += 4)
                {
                    var effect = BinaryPrimitives.ReadUInt16BigEndian(entries.Slice(i, 2));
                    var node = BinaryPrimitives.ReadUInt16BigEndian(entries.Slice(i + 2, 2));
                    if (nodeIds.Contains(node)) masteryEffects[node] = effect;
                }
            }
        }

        return nodeIds;
    }

    public static string Encode(HashSet<ushort> nodes, ESkillTreeType type,
        IReadOnlyDictionary<ushort, ushort> masteryEffects = null)
    {
        var prefix = type switch
        {
            ESkillTreeType.Atlas => "https://www.pathofexile.com/fullscreen-atlas-skill-tree/",
            ESkillTreeType.Character => "https://www.pathofexile.com/fullscreen-passive-skill-tree/",
        };
        var versionBytes = new byte[] { 0, 0, 0, 6 };
        var classBytes = new byte[] { 0 };
        var ascendancyBytes = new byte[] { 0 };
        var nodeCountBytes = new byte[] { (byte)nodes.Count };
        var nodeBytes = nodes.OrderBy(x => x).SelectMany(x => new byte[] { (byte)(x >> 8), (byte)x });
        var masteries = masteryEffects?.Where(pair => nodes.Contains(pair.Key)).OrderBy(pair => pair.Key).ToArray() ?? [];
        var tailBytes = new byte[] { 0, (byte)masteries.Length }.Concat(masteries.SelectMany(pair =>
            new byte[] { (byte)(pair.Value >> 8), (byte)pair.Value, (byte)(pair.Key >> 8), (byte)pair.Key }));
        var allBytes = versionBytes.Concat(classBytes).Concat(ascendancyBytes).Concat(nodeCountBytes).Concat(nodeBytes).Concat(tailBytes).ToArray();
        var encodedStr = Convert.ToBase64String(allBytes).Replace('+', '-').Replace('/', '_');
        return prefix + encodedStr;
    }
}
