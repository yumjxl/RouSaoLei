using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLogic.LeiPai
{
    public sealed class LeiPaiStageSummary
    {
        public int StageId { get; }
        public int Score { get; }
        public int Seed { get; }
        public int SelectedRewardCardId { get; internal set; }
        public LeiPaiStageSummary(int stageId, int score, int seed) { StageId = stageId; Score = score; Seed = seed; }
    }

    public sealed class LeiPaiRunState
    {
        private readonly List<int> _corePool = new List<int>();
        private readonly List<LeiPaiStageSummary> _summaries = new List<LeiPaiStageSummary>();
        public int RunSeed { get; internal set; }
        public int CurrentStageId { get; internal set; }
        public int CurrentBoardSeed { get; internal set; }
        public LeiPaiGameState CurrentGame { get; internal set; }
        public IReadOnlyList<int> CorePool => _corePool;
        public IReadOnlyList<LeiPaiStageSummary> Summaries => _summaries;
        public bool AwaitingReward { get; internal set; }
        public bool IsComplete { get; internal set; }
        internal List<int> MutableCorePool => _corePool;
        internal List<LeiPaiStageSummary> MutableSummaries => _summaries;
    }

    public sealed class LeiPaiRunRules
    {
        private readonly ILeiPaiLevelCatalog _levels;
        private readonly LeiPaiGameRules _gameRules;
        public LeiPaiRunRules(ILeiPaiLevelCatalog levels, ILeiPaiCardCatalog cards)
        { _levels = levels; _gameRules = new LeiPaiGameRules(cards); }

        public LeiPaiRunState StartNewRun(int seed)
        {
            LeiPaiRunState run = new LeiPaiRunState { RunSeed = seed, CurrentStageId = 1 };
            run.MutableCorePool.AddRange(_levels.Get(1).InitialCoreIds);
            StartStage(run, BoardSeed(seed, 1));
            return run;
        }

        public void RetryCurrentBoard(LeiPaiRunState run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.IsComplete || run.AwaitingReward) throw new InvalidOperationException("当前状态不能重试棋盘。");
            StartStage(run, run.CurrentBoardSeed);
        }

        public IReadOnlyList<int> CompleteStage(LeiPaiRunState run)
        {
            if (run.CurrentGame == null || !run.CurrentGame.IsWon) throw new InvalidOperationException("当前关尚未胜利。");
            run.MutableSummaries.Add(new LeiPaiStageSummary(run.CurrentStageId, run.CurrentGame.Score, run.CurrentBoardSeed));
            if (run.CurrentStageId >= _levels.StageCount)
            {
                run.IsComplete = true;
                run.CurrentGame = null;
                return Array.Empty<int>();
            }
            run.AwaitingReward = true;
            return RewardChoices(run);
        }

        public void SelectReward(LeiPaiRunState run, int cardId)
        {
            if (!run.AwaitingReward) throw new InvalidOperationException("当前不在奖励选择阶段。");
            if (!RewardChoices(run).Contains(cardId)) throw new ArgumentException("奖励不在当前候选中。", nameof(cardId));
            run.MutableCorePool.Add(cardId);
            run.MutableSummaries[run.MutableSummaries.Count - 1].SelectedRewardCardId = cardId;
            run.AwaitingReward = false;
            run.CurrentStageId++;
            StartStage(run, BoardSeed(run.RunSeed, run.CurrentStageId));
        }

        private IReadOnlyList<int> RewardChoices(LeiPaiRunState run)
        {
            List<int> pool = new List<int>(_levels.Get(run.CurrentStageId).RewardCardIds);
            Random random = new Random(unchecked(run.RunSeed + run.CurrentStageId * 104729));
            for (int i = pool.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
            return pool.Take(3).ToArray();
        }

        private void StartStage(LeiPaiRunState run, int boardSeed)
        {
            LeiPaiLevelConfig source = _levels.Get(run.CurrentStageId);
            source.CorePool = new List<int>(run.CorePool);
            run.CurrentBoardSeed = boardSeed;
            run.CurrentGame = _gameRules.Start(source, boardSeed);
        }

        private static int BoardSeed(int runSeed, int stageId) => unchecked(runSeed * 397 ^ stageId * 7919);
    }
}
