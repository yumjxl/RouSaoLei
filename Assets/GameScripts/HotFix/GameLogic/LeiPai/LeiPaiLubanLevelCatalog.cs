using System.Collections.Generic;

namespace GameLogic.LeiPai
{
    public static class LeiPaiLubanLevelCatalog
    {
        public static LeiPaiLevelCatalog Load()
        {
            Dictionary<int, LeiPaiLevelConfig> levels = new Dictionary<int, LeiPaiLevelConfig>();
            foreach (GameConfig.leipai.Level row in ConfigSystem.Instance.Tables.TbLevel.DataList)
            {
                levels.Add(row.Id, new LeiPaiLevelConfig
                {
                    Width = row.Width, Height = row.Height, MineCount = row.MineCount, ToolCount = row.ToolCount,
                    TurnCount = row.TurnCount, ShieldCount = row.ShieldCount, ScoutCount = row.ScoutCount,
                    TargetScore = row.TargetScore, BasicCardId = row.BasicCardId, MineCardId = row.MineCardId,
                    ToolCardId = row.ToolCardId, InitialCoreIds = LeiPaiLevelCatalog.ParseIds(row.InitialCoreIds),
                    RewardCardIds = LeiPaiLevelCatalog.ParseIds(row.RewardCardIds)
                });
            }
            return new LeiPaiLevelCatalog(levels);
        }
    }
}
