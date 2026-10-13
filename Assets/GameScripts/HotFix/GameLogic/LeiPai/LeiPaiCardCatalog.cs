using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GameLogic.LeiPai
{
    public sealed class LeiPaiCardConfig
    {
        private readonly Dictionary<string, float> _effects;
        public int Id { get; }
        public string Key { get; }
        public string Category { get; }
        public string Name { get; }
        public string Icon { get; }
        public string Description { get; }
        public string ClueRole { get; }
        public bool IsSafe { get; }
        public IEnumerable<KeyValuePair<string, float>> Effects => _effects;

        public LeiPaiCardConfig(int id, string key, string category, string name, string icon,
            string description, string clueRole, bool isSafe, string effects)
        {
            Id = id; Key = key; Category = category; Name = name; Icon = icon;
            Description = description; ClueRole = clueRole; IsSafe = isSafe;
            _effects = ParseEffects(effects);
        }

        public bool HasEffect(string effect) => _effects.ContainsKey(effect);
        public float Effect(string effect, float fallback = 0f)
            => _effects.TryGetValue(effect, out float value) ? value : fallback;

        private static Dictionary<string, float> ParseEffects(string source)
        {
            Dictionary<string, float> result = new Dictionary<string, float>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(source)) return result;
            string[] entries = source.Split('|');
            for (int i = 0; i < entries.Length; i++)
            {
                string[] pair = entries[i].Split(':');
                if (pair.Length != 2 || !float.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    throw new FormatException($"无效卡牌效果: {entries[i]}");
                result.Add(pair[0], value);
            }
            return result;
        }
    }

    public interface ILeiPaiCardCatalog
    {
        LeiPaiCardConfig Get(int cardId);
    }

    public sealed class LeiPaiCardCatalog : ILeiPaiCardCatalog
    {
        private readonly Dictionary<int, LeiPaiCardConfig> _cards;
        public LeiPaiCardCatalog(IEnumerable<LeiPaiCardConfig> cards) => _cards = cards.ToDictionary(card => card.Id);
        public LeiPaiCardConfig Get(int cardId)
            => _cards.TryGetValue(cardId, out LeiPaiCardConfig card) ? card : throw new KeyNotFoundException($"卡牌配置不存在: {cardId}");

        public static LeiPaiCardCatalog CreateDefaults() => new LeiPaiCardCatalog(new[]
        {
            new LeiPaiCardConfig(1001,"basic","normal","基础卡","card_basic","","none",true,"reveal.score:10|reveal.countBasic:1"),
            new LeiPaiCardConfig(1002,"mine","hazard","地雷","card_mine","","mine",false,"reveal.shield:-1|reveal.score:-20"),
            new LeiPaiCardConfig(1003,"tool","tool","工具卡","card_tool","","none",true,"reveal.turn:1"),
            new LeiPaiCardConfig(2001,"collector","core","收集器","card_collector","","core",true,"reveal.scorePerPriorBasic:12|active.chargePerBasic:4"),
            new LeiPaiCardConfig(2002,"amplifier","core","增幅器","card_amplifier","","core",true,"active.scorePerBasic:6"),
            new LeiPaiCardConfig(2003,"resonator","core","共鸣器","card_resonator","","core",true,"reveal.confirmAdjacentBasic:2|active.adjacentBasicScore:15"),
            new LeiPaiCardConfig(2004,"multiplier","core","倍频器","card_multiplier","","core",true,"active.instantMultiplier:1.5"),
            new LeiPaiCardConfig(2005,"mapper","core","测绘器","card_mapper","","core",true,"reveal.scout:2"),
            new LeiPaiCardConfig(2006,"echo","core","回响器","card_echo","","core",true,"active.everyBasic:3|active.scorePerTotalBasic:5")
        });
    }
}
