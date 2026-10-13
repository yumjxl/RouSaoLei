using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLogic.LeiPai
{
    public enum LeiPaiActionStatus
    {
        Success,
        InvalidPosition,
        AlreadyRevealed,
        Marked,
        NoScouts,
        InvalidScoutTarget,
        AlreadyScouted,
        GameEnded
    }

    public enum LeiPaiContributionKind
    {
        Basic,
        Hazard,
        Core,
        Multiplier,
        StoredScore
    }

    public sealed class LeiPaiScoreContribution
    {
        public string Source { get; }
        public int Value { get; }
        public LeiPaiContributionKind Kind { get; }
        public LeiPaiPosition? SourcePosition { get; }

        public LeiPaiScoreContribution(string source, int value, LeiPaiContributionKind kind, LeiPaiPosition? sourcePosition)
        {
            Source = source;
            Value = value;
            Kind = kind;
            SourcePosition = sourcePosition;
        }
    }

    public sealed class LeiPaiActionResult
    {
        private readonly List<LeiPaiScoreContribution> _contributions = new List<LeiPaiScoreContribution>();

        public LeiPaiActionStatus Status { get; internal set; }
        public LeiPaiCardState Card { get; internal set; }
        public int TurnDelta { get; internal set; }
        public int ShieldDelta { get; internal set; }
        public int ScoreDelta { get; internal set; }
        public IReadOnlyList<LeiPaiScoreContribution> Contributions => _contributions;

        internal void AddScore(string source, int value, LeiPaiContributionKind kind, LeiPaiPosition? sourcePosition = null)
        {
            ScoreDelta += value;
            _contributions.Add(new LeiPaiScoreContribution(source, value, kind, sourcePosition));
        }
    }

    public sealed class LeiPaiGameState
    {
        private readonly List<LeiPaiCoreRuntimeState> _activeCores = new List<LeiPaiCoreRuntimeState>();

        public LeiPaiLevelConfig Config { get; }
        public LeiPaiBoardState Board { get; }
        public int Score { get; internal set; }
        public int ChargedScore { get; internal set; }
        public int RemainingTurns { get; internal set; }
        public int Shield { get; internal set; }
        public int RemainingScouts { get; internal set; }
        public int CountedBasicCards { get; internal set; }
        public bool IsWon { get; internal set; }
        public bool IsLost { get; internal set; }
        public bool IsEnded => IsWon || IsLost;
        public IReadOnlyList<LeiPaiCoreRuntimeState> ActiveCores => _activeCores;

        internal LeiPaiGameState(LeiPaiLevelConfig config, LeiPaiBoardState board)
        {
            Config = config;
            Board = board;
            RemainingTurns = config.TurnCount;
            Shield = config.ShieldCount;
            RemainingScouts = config.ScoutCount;
        }

        internal LeiPaiCoreRuntimeState ActivateCore(LeiPaiCardState card)
        {
            LeiPaiCoreRuntimeState state = new LeiPaiCoreRuntimeState(card, _activeCores.Count);
            _activeCores.Add(state);
            return state;
        }
    }

    public sealed class LeiPaiCoreRuntimeState
    {
        public LeiPaiCardState Card { get; }
        public int ActivationOrder { get; }
        public int Charge { get; internal set; }
        public int EchoProgress { get; internal set; }

        internal LeiPaiCoreRuntimeState(LeiPaiCardState card, int activationOrder)
        {
            Card = card;
            ActivationOrder = activationOrder;
        }
    }

    public sealed class LeiPaiGameRules
    {
        private readonly ILeiPaiCardCatalog _catalog;
        public LeiPaiGameRules(ILeiPaiCardCatalog catalog = null) => _catalog = catalog ?? LeiPaiCardCatalog.CreateDefaults();

        public LeiPaiGameState Start(LeiPaiLevelConfig config, int seed)
        {
            LeiPaiBoardState board = new LeiPaiBoardGenerator(_catalog).Generate(config, seed);
            return new LeiPaiGameState(config, board);
        }

        public LeiPaiMarkType ToggleMark(LeiPaiGameState game, LeiPaiPosition position)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (game.IsEnded || !game.Board.Contains(position)) return LeiPaiMarkType.None;
            LeiPaiCardState card = game.Board.GetCard(position);
            if (card.IsRevealed) return card.Mark;

            switch (card.Mark)
            {
                case LeiPaiMarkType.None:
                    card.Mark = LeiPaiMarkType.SuspectedMine;
                    break;
                case LeiPaiMarkType.SuspectedMine:
                    card.Mark = LeiPaiMarkType.SuspectedCore;
                    break;
                default:
                    card.Mark = LeiPaiMarkType.None;
                    break;
            }
            return card.Mark;
        }

        public LeiPaiActionResult Reveal(LeiPaiGameState game, LeiPaiPosition position)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            LeiPaiActionResult result = new LeiPaiActionResult();
            if (game.IsEnded) return WithStatus(result, LeiPaiActionStatus.GameEnded);
            if (!game.Board.Contains(position)) return WithStatus(result, LeiPaiActionStatus.InvalidPosition);

            LeiPaiCardState card = game.Board.GetCard(position);
            result.Card = card;
            if (card.IsRevealed) return WithStatus(result, LeiPaiActionStatus.AlreadyRevealed);
            if (card.Mark != LeiPaiMarkType.None) return WithStatus(result, LeiPaiActionStatus.Marked);

            card.IsRevealed = true;
            game.RemainingTurns--;
            result.TurnDelta--;

            if (card.Config.HasEffect("reveal.countBasic"))
            {
                RevealBasic(game, card, result);
                ApplyMultiplier(game, result);
                AddImmediateScore(game, result.ScoreDelta);
                ChargeCollectors(game);
            }
            else if (card.Config.HasEffect("reveal.shield"))
            {
                int shieldDelta = (int)card.Config.Effect("reveal.shield");
                game.Shield += shieldDelta;
                result.ShieldDelta += shieldDelta;
                int penalty = Math.Min((int)Math.Abs(card.Config.Effect("reveal.score")), game.Score);
                result.AddScore("地雷", -penalty, LeiPaiContributionKind.Hazard, card.Position);
                AddImmediateScore(game, result.ScoreDelta);
            }
            else if (card.Config.HasEffect("reveal.turn"))
            {
                int turnDelta = (int)card.Config.Effect("reveal.turn");
                game.RemainingTurns += turnDelta;
                result.TurnDelta += turnDelta;
            }
            else
            {
                RevealCore(game, card, result);
                ApplyMultiplier(game, result);
                AddImmediateScore(game, result.ScoreDelta);
            }

            UpdateEndState(game, result);
            result.Status = LeiPaiActionStatus.Success;
            return result;
        }

        public LeiPaiActionResult ScoutCard(LeiPaiGameState game, LeiPaiPosition position)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            LeiPaiActionResult result = new LeiPaiActionResult();
            if (game.IsEnded) return WithStatus(result, LeiPaiActionStatus.GameEnded);
            if (!game.Board.Contains(position)) return WithStatus(result, LeiPaiActionStatus.InvalidPosition);
            if (game.RemainingScouts <= 0) return WithStatus(result, LeiPaiActionStatus.NoScouts);

            LeiPaiCardState card = game.Board.GetCard(position);
            result.Card = card;
            if (card.IsRevealed) return WithStatus(result, LeiPaiActionStatus.InvalidScoutTarget);
            if (card.IsScouted) return WithStatus(result, LeiPaiActionStatus.AlreadyScouted);

            card.IsScouted = true;
            game.RemainingScouts--;
            result.Status = LeiPaiActionStatus.Success;
            return result;
        }

        public LeiPaiActionResult SplitClue(LeiPaiGameState game, LeiPaiPosition position)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            LeiPaiActionResult result = new LeiPaiActionResult();
            if (game.IsEnded) return WithStatus(result, LeiPaiActionStatus.GameEnded);
            if (!game.Board.Contains(position)) return WithStatus(result, LeiPaiActionStatus.InvalidPosition);
            if (game.RemainingScouts <= 0) return WithStatus(result, LeiPaiActionStatus.NoScouts);

            LeiPaiCardState card = game.Board.GetCard(position);
            result.Card = card;
            if (!card.IsRevealed || card.Clue.Kind != LeiPaiClueKind.Mixed)
            {
                return WithStatus(result, LeiPaiActionStatus.InvalidScoutTarget);
            }
            if (card.IsClueSplit) return WithStatus(result, LeiPaiActionStatus.AlreadyScouted);

            card.IsClueSplit = true;
            game.RemainingScouts--;
            result.Status = LeiPaiActionStatus.Success;
            return result;
        }

        private static LeiPaiActionResult WithStatus(LeiPaiActionResult result, LeiPaiActionStatus status)
        {
            result.Status = status;
            return result;
        }

        private static void RevealBasic(LeiPaiGameState game, LeiPaiCardState card, LeiPaiActionResult result)
        {
            card.HasCountedAsBasic = true;
            game.CountedBasicCards++;
            result.AddScore("基础卡", 10, LeiPaiContributionKind.Basic, card.Position);

            for (int i = 0; i < game.ActiveCores.Count; i++)
            {
                LeiPaiCoreRuntimeState core = game.ActiveCores[i];
                int flatBonus = (int)core.Card.Config.Effect("active.scorePerBasic");
                if (flatBonus != 0) result.AddScore($"{core.Card.Config.Name} #{core.ActivationOrder + 1}", flatBonus, LeiPaiContributionKind.Core, core.Card.Position);
                int adjacentScore = (int)core.Card.Config.Effect("active.adjacentBasicScore");
                if (adjacentScore != 0 && AreAdjacent(core.Card.Position, card.Position))
                    result.AddScore($"{core.Card.Config.Name} #{core.ActivationOrder + 1}", CountAdjacentCountedBasics(game.Board, core.Card.Position) * adjacentScore, LeiPaiContributionKind.Core, core.Card.Position);
                int interval = (int)core.Card.Config.Effect("active.everyBasic");
                if (interval > 0 && ++core.EchoProgress >= interval)
                {
                    core.EchoProgress -= interval;
                    result.AddScore($"{core.Card.Config.Name} #{core.ActivationOrder + 1}", game.CountedBasicCards * (int)core.Card.Config.Effect("active.scorePerTotalBasic"), LeiPaiContributionKind.Core, core.Card.Position);
                }
            }
        }

        private static void RevealCore(LeiPaiGameState game, LeiPaiCardState card, LeiPaiActionResult result)
        {
            int priorBasicScore = (int)card.Config.Effect("reveal.scorePerPriorBasic");
            if (priorBasicScore != 0) result.AddScore($"{card.Config.Name}翻开", game.CountedBasicCards * priorBasicScore, LeiPaiContributionKind.Core, card.Position);
            int confirmCount = (int)card.Config.Effect("reveal.confirmAdjacentBasic");
            if (confirmCount > 0)
            {
                ConfirmAdjacentBasics(game.Board, card.Position, confirmCount);
                result.AddScore($"{card.Config.Name}翻开", CountAdjacentCountedBasics(game.Board, card.Position) * (int)card.Config.Effect("active.adjacentBasicScore"), LeiPaiContributionKind.Core, card.Position);
            }
            game.RemainingScouts += (int)card.Config.Effect("reveal.scout");
            if (card.Config.Effects.Any(effect => effect.Key.StartsWith("active.", StringComparison.Ordinal))) game.ActivateCore(card);
        }

        private static void ApplyMultiplier(LeiPaiGameState game, LeiPaiActionResult result)
        {
            if (result.ScoreDelta <= 0) return;
            double multiplier = 1d;
            for (int i = 0; i < game.ActiveCores.Count; i++)
            {
                float value = game.ActiveCores[i].Card.Config.Effect("active.instantMultiplier", 1f);
                multiplier *= value;
            }
            if (result.Card != null && result.Card.Config.HasEffect("active.instantMultiplier")) multiplier /= result.Card.Config.Effect("active.instantMultiplier", 1f);
            if (multiplier <= 1d) return;

            int beforeMultiplier = result.ScoreDelta;
            int afterMultiplier = (int)Math.Floor(beforeMultiplier * multiplier);
            LeiPaiCoreRuntimeState multiplierCore = game.ActiveCores.FirstOrDefault(core => core.Card.Config.HasEffect("active.instantMultiplier"));
            result.AddScore($"倍率 ×{multiplier:0.##}", afterMultiplier - beforeMultiplier, LeiPaiContributionKind.Multiplier, multiplierCore?.Card.Position);
        }

        private static void ChargeCollectors(LeiPaiGameState game)
        {
            for (int i = 0; i < game.ActiveCores.Count; i++)
            {
                LeiPaiCoreRuntimeState core = game.ActiveCores[i];
                int charge = (int)core.Card.Config.Effect("active.chargePerBasic");
                if (charge == 0) continue;
                core.Charge += charge;
                game.ChargedScore += charge;
            }
        }

        private static void AddImmediateScore(LeiPaiGameState game, int delta)
        {
            game.Score = Math.Max(0, game.Score + delta);
        }

        private static void ConfirmAdjacentBasics(LeiPaiBoardState board, LeiPaiPosition center, int limit)
        {
            int confirmed = 0;
            LeiPaiBoardGenerator.ForEachNeighbor(board, center, card =>
            {
                if (confirmed >= limit || card.IsRevealed || !card.Config.HasEffect("reveal.countBasic")) return;
                card.IsSafeConfirmed = true;
                confirmed++;
            });
        }

        private static int CountAdjacentCountedBasics(LeiPaiBoardState board, LeiPaiPosition center)
        {
            int count = 0;
            LeiPaiBoardGenerator.ForEachNeighbor(board, center, card =>
            {
                if (card.IsRevealed && card.HasCountedAsBasic) count++;
            });
            return count;
        }

        private static bool AreAdjacent(LeiPaiPosition first, LeiPaiPosition second)
            => Math.Abs(first.X - second.X) <= 1 && Math.Abs(first.Y - second.Y) <= 1 && !first.Equals(second);

        private static void UpdateEndState(LeiPaiGameState game, LeiPaiActionResult result)
        {
            if (game.Shield <= 0)
            {
                game.IsLost = true;
                return;
            }
            if (game.Score + game.ChargedScore >= game.Config.TargetScore)
            {
                int chargedScore = game.ChargedScore;
                if (chargedScore > 0)
                {
                    game.Score += chargedScore;
                    game.ChargedScore = 0;
                    for (int i = 0; i < game.ActiveCores.Count; i++) game.ActiveCores[i].Charge = 0;
                    LeiPaiCoreRuntimeState collector = game.ActiveCores.FirstOrDefault(core => core.Card.Config.HasEffect("active.chargePerBasic"));
                    result.AddScore("收集器蓄能兑现", chargedScore, LeiPaiContributionKind.StoredScore, collector?.Card.Position);
                }
                game.IsWon = true;
                return;
            }
            if (game.RemainingTurns <= 0) game.IsLost = true;
        }
    }
}
