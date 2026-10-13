using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLogic.LeiPai
{
    public interface ILeiPaiLevelCatalog
    {
        LeiPaiLevelConfig Get(int stageId);
        int StageCount { get; }
    }

    public sealed class LeiPaiLevelCatalog : ILeiPaiLevelCatalog
    {
        private readonly Dictionary<int, LeiPaiLevelConfig> _levels;
        public int StageCount => _levels.Count;

        public LeiPaiLevelCatalog(Dictionary<int, LeiPaiLevelConfig> levels) => _levels = levels;
        public LeiPaiLevelConfig Get(int stageId)
            => _levels.TryGetValue(stageId, out LeiPaiLevelConfig level) ? level : throw new KeyNotFoundException($"关卡配置不存在: {stageId}");

        public static List<int> ParseIds(string source)
            => string.IsNullOrWhiteSpace(source)
                ? new List<int>()
                : source.Split('|').Select(value => int.Parse(value)).ToList();

        public static LeiPaiLevelCatalog CreateTestDefaults() => new LeiPaiLevelCatalog(new Dictionary<int, LeiPaiLevelConfig>
        {
            [1] = Create(240, new[]{2001,2002,2003}, new[]{2004,2005,2006}),
            [2] = Create(320, Array.Empty<int>(), new[]{2001,2002,2003,2004,2005,2006}),
            [3] = Create(400, Array.Empty<int>(), Array.Empty<int>())
        });

        private static LeiPaiLevelConfig Create(int target, IEnumerable<int> initial, IEnumerable<int> rewards)
            => new LeiPaiLevelConfig { TargetScore = target, InitialCoreIds = initial.ToList(), RewardCardIds = rewards.ToList() };
    }
}
