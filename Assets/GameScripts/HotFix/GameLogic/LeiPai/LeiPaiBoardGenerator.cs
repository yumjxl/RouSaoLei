using System;
using System.Collections.Generic;

namespace GameLogic.LeiPai
{
    public sealed class LeiPaiBoardGenerator
    {
        private readonly ILeiPaiCardCatalog _catalog;
        private const int MAX_GENERATION_ATTEMPTS = 1024;
        private static readonly int[] NEIGHBOR_X = { -1, 0, 1, -1, 1, -1, 0, 1 };
        private static readonly int[] NEIGHBOR_Y = { -1, -1, -1, 0, 0, 1, 1, 1 };

        public LeiPaiBoardGenerator(ILeiPaiCardCatalog catalog) => _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

        public LeiPaiBoardState Generate(LeiPaiLevelConfig config, int seed)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Validate();

            List<int> pool = BuildPool(config);
            for (int attempt = 0; attempt < MAX_GENERATION_ATTEMPTS; attempt++)
            {
                int attemptSeed = unchecked(seed + attempt * 7919);
                Random random = new Random(attemptSeed);
                Shuffle(pool, random);
                LeiPaiBoardState board = CreateBoard(config, seed, pool);
                CalculateClues(board);

                List<LeiPaiCardState> entryCandidates = GetEntryCandidates(board);
                if (entryCandidates.Count == 0 || !HasValidResonators(board)) continue;

                LeiPaiCardState entry = entryCandidates[random.Next(entryCandidates.Count)];
                entry.IsEntry = true;
                entry.IsRevealed = true;
                board.EntryPosition = entry.Position;
                return board;
            }

            throw new InvalidOperationException($"种子 {seed} 在 {MAX_GENERATION_ATTEMPTS} 次尝试后仍无法生成合法棋盘。");
        }

        private static List<int> BuildPool(LeiPaiLevelConfig config)
        {
            List<int> pool = new List<int>(config.CellCount);
            Add(pool, config.MineCardId, config.MineCount);
            Add(pool, config.ToolCardId, config.ToolCount);
            pool.AddRange(config.CorePool);
            Add(pool, config.BasicCardId, config.BasicCount);
            return pool;
        }

        private static void Add(List<int> pool, int cardId, int count)
        {
            for (int i = 0; i < count; i++) pool.Add(cardId);
        }

        private static void Shuffle(List<int> pool, Random random)
        {
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int index = random.Next(i + 1);
                int value = pool[i];
                pool[i] = pool[index];
                pool[index] = value;
            }
        }

        private LeiPaiBoardState CreateBoard(LeiPaiLevelConfig config, int seed, List<int> pool)
        {
            LeiPaiCardState[] cards = new LeiPaiCardState[pool.Count];
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = new LeiPaiCardState(new LeiPaiPosition(i % config.Width, i / config.Width), _catalog.Get(pool[i]));
            }
            return new LeiPaiBoardState(config.Width, config.Height, seed, cards);
        }

        private static void CalculateClues(LeiPaiBoardState board)
        {
            for (int i = 0; i < board.Cards.Count; i++)
            {
                LeiPaiCardState card = board.Cards[i];
                LeiPaiClue clue = new LeiPaiClue();
                ForEachNeighbor(board, card.Position, neighbor =>
                {
                    if (neighbor.Config.ClueRole == "mine") clue.MineCount++;
                    else if (neighbor.Config.ClueRole == "core") clue.CoreCount++;
                });
                card.Clue = clue;
            }
        }

        private static List<LeiPaiCardState> GetEntryCandidates(LeiPaiBoardState board)
        {
            List<LeiPaiCardState> candidates = new List<LeiPaiCardState>();
            for (int i = 0; i < board.Cards.Count; i++)
            {
                LeiPaiCardState card = board.Cards[i];
                if (!card.Config.HasEffect("reveal.countBasic")) continue;
                bool hasSafeNeighbor = false;
                ForEachNeighbor(board, card.Position, neighbor => hasSafeNeighbor |= neighbor.Config.IsSafe);
                if (hasSafeNeighbor) candidates.Add(card);
            }
            return candidates;
        }

        private static bool HasValidResonators(LeiPaiBoardState board)
        {
            for (int i = 0; i < board.Cards.Count; i++)
            {
                LeiPaiCardState card = board.Cards[i];
                if (!card.Config.HasEffect("reveal.confirmAdjacentBasic")) continue;
                int adjacentBasicCount = 0;
                ForEachNeighbor(board, card.Position, neighbor =>
                {
                    if (neighbor.Config.HasEffect("reveal.countBasic")) adjacentBasicCount++;
                });
                if (adjacentBasicCount < 2) return false;
            }
            return true;
        }

        public static void ForEachNeighbor(LeiPaiBoardState board, LeiPaiPosition center, Action<LeiPaiCardState> action)
        {
            for (int i = 0; i < NEIGHBOR_X.Length; i++)
            {
                LeiPaiPosition position = new LeiPaiPosition(center.X + NEIGHBOR_X[i], center.Y + NEIGHBOR_Y[i]);
                if (board.Contains(position)) action(board.GetCard(position));
            }
        }
    }
}
