using ExileCore;
using PassiveSkillTreePlanter.UrlDecoders;
using System;
using System.Collections.Generic;

namespace PassiveSkillTreePlanter;

public class TreeEncoder
{
    public static (HashSet<ushort> Nodes, ESkillTreeType Type, Dictionary<ushort, ushort> MasteryEffects) DecodeUrl(string url)
    {
        try
        {
            if (PoePlannerUrlDecoder.TryMatch(url, out var type, out var passiveIds))
            {
                return (passiveIds, type, new());
            }
            if (PathOfExileUrlDecoder.TryMatch(url, out type, out passiveIds, out var masteryEffects))
            {
                return (passiveIds, type, masteryEffects);
            }
        }
        catch (Exception ex)
        {
            DebugWindow.LogError($"Failed to decode url {url}:\n{ex}");
            return default;
        }

        return default;
    }
}
