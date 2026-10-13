using System.Linq;
using GameLogic.LeiPai;
using NUnit.Framework;

namespace GameLogic.Tests.LeiPai
{
    public sealed class LeiPaiRulesTests
    {
        private static readonly ILeiPaiCardCatalog Catalog = LeiPaiCardCatalog.CreateDefaults();
        [Test]
        public void Generate_WithSameSeed_ProducesSameBoard()
        {
            LeiPaiLevelConfig config = CreateConfig();
            LeiPaiBoardGenerator generator = new LeiPaiBoardGenerator(Catalog);

            LeiPaiBoardState first = generator.Generate(config, 20261011);
            LeiPaiBoardState second = generator.Generate(config, 20261011);

            CollectionAssert.AreEqual(first.Cards.Select(card => card.CardId), second.Cards.Select(card => card.CardId));
            Assert.AreEqual(first.EntryPosition, second.EntryPosition);
        }

        [Test]
        public void Generate_DefaultConfig_HasCorrectCompositionAndSafeEntry()
        {
            LeiPaiLevelConfig config = CreateConfig();
            LeiPaiBoardState board = new LeiPaiBoardGenerator(Catalog).Generate(config, 7);

            Assert.AreEqual(36, board.Cards.Count);
            Assert.AreEqual(6, board.Cards.Count(card => card.CardId == 1002));
            Assert.AreEqual(2, board.Cards.Count(card => card.CardId == 1003));
            Assert.AreEqual(3, board.Cards.Count(card => card.Config.ClueRole == "core"));
            Assert.AreEqual(25, board.Cards.Count(card => card.CardId == 1001));

            LeiPaiCardState entry = board.GetCard(board.EntryPosition);
            Assert.AreEqual(1001, entry.CardId);
            Assert.IsTrue(entry.IsEntry);
            Assert.IsTrue(entry.IsRevealed);
            Assert.IsTrue(Neighbors(board, entry).Any(card => card.Config.IsSafe));
        }

        [Test]
        public void Generate_CluesAndResonatorConstraint_MatchLayout()
        {
            LeiPaiBoardState board = new LeiPaiBoardGenerator(Catalog).Generate(CreateConfig(), 99);

            foreach (LeiPaiCardState card in board.Cards)
            {
                LeiPaiCardState[] neighbors = Neighbors(board, card).ToArray();
                Assert.AreEqual(neighbors.Count(value => value.Config.ClueRole == "mine"), card.Clue.MineCount);
                Assert.AreEqual(neighbors.Count(value => value.Config.ClueRole == "core"), card.Clue.CoreCount);
            }

            foreach (LeiPaiCardState resonator in board.Cards.Where(card => card.Config.HasEffect("reveal.confirmAdjacentBasic")))
            {
                Assert.GreaterOrEqual(Neighbors(board, resonator).Count(card => card.Config.HasEffect("reveal.countBasic")), 2);
            }
        }

        [Test]
        public void Reveal_EntryDoesNotCountAndToolRefundsTurn()
        {
            LeiPaiGameRules rules = new LeiPaiGameRules();
            LeiPaiGameState game = rules.Start(CreateConfig(), 42);

            Assert.AreEqual(0, game.Score);
            Assert.AreEqual(0, game.CountedBasicCards);
            Assert.AreEqual(24, game.RemainingTurns);

            LeiPaiCardState tool = game.Board.Cards.First(card => card.CardId == 1003);
            LeiPaiActionResult result = rules.Reveal(game, tool.Position);

            Assert.AreEqual(LeiPaiActionStatus.Success, result.Status);
            Assert.AreEqual(0, result.TurnDelta);
            Assert.AreEqual(24, game.RemainingTurns);
        }

        [Test]
        public void Reveal_MineUsesShieldAndScoreNeverDropsBelowZero()
        {
            LeiPaiGameRules rules = new LeiPaiGameRules();
            LeiPaiGameState game = rules.Start(CreateConfig(), 123);
            LeiPaiCardState mine = game.Board.Cards.First(card => card.CardId == 1002);

            LeiPaiActionResult result = rules.Reveal(game, mine.Position);

            Assert.AreEqual(-1, result.ShieldDelta);
            Assert.AreEqual(2, game.Shield);
            Assert.AreEqual(0, game.Score);
        }

        [Test]
        public void ScoutCard_RevealsOnlyTypeAndDoesNotConsumeTurn()
        {
            LeiPaiGameRules rules = new LeiPaiGameRules();
            LeiPaiGameState game = rules.Start(CreateConfig(), 456);
            LeiPaiCardState hidden = game.Board.Cards.First(card => !card.IsRevealed);

            LeiPaiActionResult result = rules.ScoutCard(game, hidden.Position);

            Assert.AreEqual(LeiPaiActionStatus.Success, result.Status);
            Assert.IsTrue(hidden.IsScouted);
            Assert.IsFalse(hidden.IsRevealed);
            Assert.AreEqual(2, game.RemainingScouts);
            Assert.AreEqual(24, game.RemainingTurns);
        }

        [Test]
        public void SplitClue_InvalidTargetDoesNotConsumeScout()
        {
            LeiPaiGameRules rules = new LeiPaiGameRules();
            LeiPaiGameState game = rules.Start(CreateConfig(), 789);
            LeiPaiCardState hidden = game.Board.Cards.First(card => !card.IsRevealed);

            LeiPaiActionResult result = rules.SplitClue(game, hidden.Position);

            Assert.AreEqual(LeiPaiActionStatus.InvalidScoutTarget, result.Status);
            Assert.AreEqual(3, game.RemainingScouts);
        }

        [Test]
        public void Reveal_AmplifierThenBasic_AddsSixBonusScore()
        {
            LeiPaiLevelConfig config = CreateConfig();
            config.TargetScore = 9999;
            LeiPaiGameRules rules = new LeiPaiGameRules();
            LeiPaiGameState game = rules.Start(config, 321);
            LeiPaiCardState amplifier = game.Board.Cards.First(card => card.CardId == 2002);
            LeiPaiCardState basic = game.Board.Cards.First(card => card.CardId == 1001 && !card.IsRevealed);

            rules.Reveal(game, amplifier.Position);
            LeiPaiActionResult result = rules.Reveal(game, basic.Position);

            Assert.AreEqual(16, result.ScoreDelta);
            Assert.AreEqual(16, game.Score);
            Assert.AreEqual(LeiPaiContributionKind.Basic, result.Contributions[0].Kind);
            Assert.AreEqual(basic.Position, result.Contributions[0].SourcePosition.Value);
            Assert.AreEqual(LeiPaiContributionKind.Core, result.Contributions[1].Kind);
            Assert.AreEqual(amplifier.Position, result.Contributions[1].SourcePosition.Value);
        }

        [Test]
        public void Reveal_CollectorThenBasic_AddsChargeOutsideImmediateScore()
        {
            LeiPaiLevelConfig config = CreateConfig();
            config.TargetScore = 9999;
            LeiPaiGameRules rules = new LeiPaiGameRules();
            LeiPaiGameState game = rules.Start(config, 654);
            LeiPaiCardState collector = game.Board.Cards.First(card => card.CardId == 2001);
            LeiPaiCardState basic = game.Board.Cards.First(card => card.CardId == 1001 && !card.IsRevealed);

            rules.Reveal(game, collector.Position);
            LeiPaiActionResult result = rules.Reveal(game, basic.Position);

            Assert.AreEqual(10, result.ScoreDelta);
            Assert.AreEqual(10, game.Score);
            Assert.AreEqual(4, game.ChargedScore);
        }

        private static LeiPaiLevelConfig CreateConfig() => new LeiPaiLevelConfig();

        private static System.Collections.Generic.IEnumerable<LeiPaiCardState> Neighbors(
            LeiPaiBoardState board, LeiPaiCardState center)
        {
            System.Collections.Generic.List<LeiPaiCardState> result = new System.Collections.Generic.List<LeiPaiCardState>();
            LeiPaiBoardGenerator.ForEachNeighbor(board, center.Position, result.Add);
            return result;
        }
    }
}
