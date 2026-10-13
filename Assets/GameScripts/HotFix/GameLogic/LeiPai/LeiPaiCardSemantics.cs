using System;

namespace GameLogic.LeiPai
{
    /// <summary>由配置字段推导稳定语义，不枚举具体卡牌品种。</summary>
    public static class LeiPaiCardSemantics
    {
        public static bool IsCore(this LeiPaiCardState card)
            => card?.Config != null && card.Config.IsCore();

        public static bool IsCore(this LeiPaiCardConfig config)
            => config != null && (HasCategory(config, "core") || HasClueRole(config, "core"));

        public static bool IsMine(this LeiPaiCardConfig config)
            => config != null && HasClueRole(config, "mine");

        public static bool HasCategory(this LeiPaiCardConfig config, string category)
            => config != null && string.Equals(config.Category, category, StringComparison.OrdinalIgnoreCase);

        public static bool HasClueRole(this LeiPaiCardConfig config, string role)
            => config != null && string.Equals(config.ClueRole, role, StringComparison.OrdinalIgnoreCase);
    }
}
