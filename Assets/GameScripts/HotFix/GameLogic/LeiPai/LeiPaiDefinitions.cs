using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.LeiPai
{
    [Serializable]
    public sealed class LeiPaiLevelConfig
    {
        [Min(2)] public int Width = 6;
        [Min(2)] public int Height = 6;
        [Min(0)] public int MineCount = 6;
        [Min(0)] public int ToolCount = 2;
        public int BasicCardId = 1001;
        public int MineCardId = 1002;
        public int ToolCardId = 1003;
        [Min(1)] public int TurnCount = 24;
        [Min(1)] public int ShieldCount = 3;
        [Min(0)] public int ScoutCount = 3;
        [Min(1)] public int TargetScore = 240;
        public List<int> CorePool = new List<int>
        {
            2001, 2002, 2003
        };
        public List<int> InitialCoreIds = new List<int>();
        public List<int> RewardCardIds = new List<int>();

        public int CellCount => Width * Height;
        public int BasicCount => CellCount - MineCount - ToolCount - CorePool.Count;

        public void Validate()
        {
            if (Width < 2 || Height < 2) throw new InvalidOperationException("棋盘尺寸至少为 2×2。");
            if (MineCount < 0 || ToolCount < 0) throw new InvalidOperationException("卡牌数量不能为负数。");
            if (CorePool == null) throw new InvalidOperationException("核心卡池不能为空。");
            if (BasicCount < 2) throw new InvalidOperationException("卡池必须至少保留两张基础卡。");
        }
    }

    [CreateAssetMenu(fileName = "LeiPaiLevelDefinition", menuName = "LeiPai/Level Definition")]
    public sealed class LeiPaiLevelDefinition : ScriptableObject
    {
        [SerializeField] private int _stage = 1;
        [SerializeField] private LeiPaiLevelConfig _config = new LeiPaiLevelConfig();

        public int Stage => _stage;
        public LeiPaiLevelConfig Config => _config;

        private void OnValidate()
        {
            _stage = Mathf.Max(1, _stage);
        }
    }
}
