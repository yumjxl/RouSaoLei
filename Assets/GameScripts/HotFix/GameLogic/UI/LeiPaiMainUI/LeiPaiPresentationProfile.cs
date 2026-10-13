using GameLogic.LeiPai;
using UnityEngine;

namespace GameLogic
{
    internal enum LeiPaiUiCue
    {
        Flip,
        Mark,
        Scout,
        Hazard,
        Goal
    }

    internal readonly struct LeiPaiPresentationStyle
    {
        public readonly string SoundAddress;
        public readonly float Volume;
        public readonly Color HighlightColor;

        public LeiPaiPresentationStyle(string soundAddress, float volume, Color highlightColor)
        {
            SoundAddress = soundAddress;
            Volume = volume;
            HighlightColor = highlightColor;
        }
    }

    /// <summary>
    /// 雷牌表现映射的唯一入口。规则只输出语义类型，UI 不解析显示文字。
    /// 后续可将这里的值迁移到 Luban 表，而不改窗口播放流程。
    /// </summary>
    internal static class LeiPaiPresentationProfile
    {
        public static LeiPaiPresentationStyle Get(LeiPaiUiCue cue)
        {
            switch (cue)
            {
                case LeiPaiUiCue.Mark: return Style("LeiPaiMark", 0.7f, 1f, 0.73f, 0.22f);
                case LeiPaiUiCue.Scout: return Style("LeiPaiScout", 0.72f, 0.28f, 0.77f, 1f);
                case LeiPaiUiCue.Hazard: return Style("LeiPaiMine", 0.86f, 1f, 0.2f, 0.25f);
                case LeiPaiUiCue.Goal: return Style("LeiPaiWin", 0.82f, 0.3f, 0.95f, 0.58f);
                default: return Style("LeiPaiFlip", 0.75f, 1f, 0.86f, 0.3f);
            }
        }

        public static LeiPaiPresentationStyle Get(LeiPaiContributionKind kind)
        {
            switch (kind)
            {
                case LeiPaiContributionKind.Hazard: return Style("LeiPaiMine", 0.72f, 1f, 0.2f, 0.25f);
                case LeiPaiContributionKind.Multiplier: return Style("LeiPaiMultiplier", 0.76f, 0.82f, 0.46f, 1f);
                case LeiPaiContributionKind.Core:
                case LeiPaiContributionKind.StoredScore: return Style("LeiPaiCore", 0.7f, 1f, 0.72f, 0.2f);
                default: return Style("LeiPaiScore", 0.62f, 1f, 0.86f, 0.3f);
            }
        }

        private static LeiPaiPresentationStyle Style(string sound, float volume, float red, float green, float blue)
            => new LeiPaiPresentationStyle(sound, volume, new Color(red, green, blue, 1f));
    }
}
