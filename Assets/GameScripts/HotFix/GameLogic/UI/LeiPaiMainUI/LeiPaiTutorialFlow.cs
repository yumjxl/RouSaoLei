using GameLogic.LeiPai;

namespace GameLogic
{
    /// <summary>教学流程状态机。只描述允许的语义操作，不依赖任何 Unity 视图。</summary>
    internal sealed class LeiPaiTutorialFlow
    {
        public const int BoardSeed = 20261011;

        public bool IsActive { get; private set; }
        public int Step { get; private set; }
        public bool IsComplete => Step >= 4;
        public string PrimaryButtonText => IsComplete ? "开始远征" : "跳过教学";

        public void Begin()
        {
            IsActive = true;
            Step = 0;
        }

        public void Leave()
        {
            IsActive = false;
            Step = 0;
        }

        public bool AllowsMark => !IsActive || Step == 1;
        public bool AllowsScoutToggle => !IsActive || Step == 2;

        public bool AllowsLeftClick(bool scoutMode)
        {
            if (!IsActive) return true;
            if (Step == 0 || Step >= 3) return !scoutMode;
            return Step == 2 && scoutMode;
        }

        public bool NotifyMarked(LeiPaiMarkType mark)
        {
            if (!IsActive || Step != 1 || mark == LeiPaiMarkType.None) return false;
            Step = 2;
            return true;
        }

        public bool NotifyAction(LeiPaiActionResult result, bool scoutMode)
        {
            if (!IsActive) return false;
            int previous = Step;
            if (Step == 0 && !scoutMode) Step = 1;
            else if (Step == 2 && scoutMode) Step = 3;
            else if (Step == 3 && result.Card.IsCore()) Step = 4;
            return previous != Step;
        }

        public void NotifyBoardEnded()
        {
            if (IsActive) Step = 4;
        }

        public string Prompt()
        {
            switch (Step)
            {
                case 0: return "教学 1/4：观察入口线索，然后左键翻开一张未标记卡牌。";
                case 1: return "教学 2/4：右键一张未翻卡，添加疑似地雷或疑似核心标记。";
                case 2: return "教学 3/4：开启侦察模式，再点击未翻卡确认身份，或拆分已翻开的混合线索。";
                case 3: return "教学 4/4：侦察已结束。翻开一张核心卡，观察逐项结算。";
                default: return "教学完成！点击“开始远征”进入全新的随机三关；也可以继续观察当前固定牌局。";
            }
        }
    }
}
