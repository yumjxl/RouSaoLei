using System;
using System.Collections.Generic;

namespace GameLogic.LeiPai
{
    public enum LeiPaiMarkType
    {
        None,
        SuspectedMine,
        SuspectedCore
    }

    public enum LeiPaiClueKind
    {
        Empty,
        MineOnly,
        CoreOnly,
        Mixed
    }

    [Serializable]
    public struct LeiPaiPosition : IEquatable<LeiPaiPosition>
    {
        public int X;
        public int Y;

        public LeiPaiPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(LeiPaiPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is LeiPaiPosition other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X}, {Y})";
    }

    [Serializable]
    public struct LeiPaiClue
    {
        public int MineCount;
        public int CoreCount;
        public int TotalCount => MineCount + CoreCount;

        public LeiPaiClueKind Kind
        {
            get
            {
                if (MineCount == 0 && CoreCount == 0) return LeiPaiClueKind.Empty;
                if (CoreCount == 0) return LeiPaiClueKind.MineOnly;
                if (MineCount == 0) return LeiPaiClueKind.CoreOnly;
                return LeiPaiClueKind.Mixed;
            }
        }
    }

    [Serializable]
    public sealed class LeiPaiCardState
    {
        public LeiPaiPosition Position { get; }
        public int CardId => Config.Id;
        public LeiPaiCardConfig Config { get; }
        public LeiPaiClue Clue { get; internal set; }
        public LeiPaiMarkType Mark { get; internal set; }
        public bool IsRevealed { get; internal set; }
        public bool IsEntry { get; internal set; }
        public bool IsScouted { get; internal set; }
        public bool IsClueSplit { get; internal set; }
        public bool IsSafeConfirmed { get; internal set; }
        public bool HasCountedAsBasic { get; internal set; }

        public LeiPaiCardState(LeiPaiPosition position, LeiPaiCardConfig config)
        {
            Position = position;
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }
    }

    public sealed class LeiPaiBoardState
    {
        private readonly LeiPaiCardState[] _cards;

        public int Width { get; }
        public int Height { get; }
        public int Seed { get; }
        public LeiPaiPosition EntryPosition { get; internal set; }
        public IReadOnlyList<LeiPaiCardState> Cards => _cards;

        internal LeiPaiBoardState(int width, int height, int seed, LeiPaiCardState[] cards)
        {
            Width = width;
            Height = height;
            Seed = seed;
            _cards = cards;
        }

        public bool Contains(LeiPaiPosition position)
            => position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;

        public LeiPaiCardState GetCard(LeiPaiPosition position)
        {
            if (!Contains(position)) throw new ArgumentOutOfRangeException(nameof(position));
            return _cards[position.Y * Width + position.X];
        }
    }

}
