using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ZeroBloat.Tweaks
{
    /// <summary>
    /// Base for tweaks that need multiple fallback strategies — Recall,
    /// Local Account De-Link, Copilot — because a single Windows Update
    /// can close one route while leaving others intact. Apply/Verify/Revert
    /// try each applicable strategy in priority order and report which one
    /// actually worked, rather than assuming a single fixed mechanism.
    /// </summary>
    public abstract class MultiStrategyTweakBase : IMultiStrategyTweak
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public abstract TweakRiskTier RiskTier { get; }
        public abstract IReadOnlyList<ITweakStrategy> Strategies { get; }

        protected abstract int CurrentBuildNumber { get; }

        private IEnumerable<ITweakStrategy> ApplicableStrategies =>
            Strategies.Where(s => s.IsApplicableToBuild(CurrentBuildNumber));

        public virtual TweakResult PreviewChange()
        {
            var strategy = ApplicableStrategies.FirstOrDefault();
            if (strategy == null)
            {
                return new TweakResult
                {
                    Success = false,
                    Message = $"No applicable strategy found for {DisplayName} on this Windows build."
                };
            }

            // Preview shows what the first applicable strategy would do.
            // Individual strategies don't implement PreviewChange directly
            // (kept out of ITweakStrategy to keep it lean) — Apply()'s
            // dry-run isn't separately modeled here yet; this reports intent only.
            return new TweakResult
            {
                Success = true,
                TargetPath = $"{DisplayName} (strategy: {strategy.StrategyName})",
                Message = $"Would attempt strategy '{strategy.StrategyName}' first, " +
                          $"with {ApplicableStrategies.Count() - 1} fallback(s) available."
            };
        }

        public virtual TweakResult Apply()
        {
            var sw = Stopwatch.StartNew();
            var applicable = ApplicableStrategies.ToList();

            if (applicable.Count == 0)
            {
                sw.Stop();
                return new TweakResult
                {
                    Success = false,
                    Duration = sw.Elapsed,
                    Message = $"No applicable strategy found for {DisplayName} on this Windows build " +
                              $"({CurrentBuildNumber}). Marking as unsupported rather than guessing."
                };
            }

            var attemptLog = new List<string>();

            foreach (var strategy in applicable)
            {
                var result = strategy.Apply();
                attemptLog.Add($"{strategy.StrategyName}: {(result.Success ? "OK" : "failed - " + result.Message)}");

                if (result.Success)
                {
                    sw.Stop();
                    MultiStrategyUndoLog.RecordUsedStrategy(Id, strategy.StrategyName);

                    return new TweakResult
                    {
                        Success = true,
                        TargetPath = result.TargetPath,
                        OldValue = result.OldValue,
                        NewValue = result.NewValue,
                        Duration = sw.Elapsed,
                        Message = $"Applied {DisplayName} via strategy '{strategy.StrategyName}'" +
                                  (attemptLog.Count > 1 ? $" (after {attemptLog.Count - 1} failed attempt(s))" : "") +
                                  $".\nAttempt log: {string.Join(" | ", attemptLog)}"
                    };
                }
            }

            sw.Stop();
            return new TweakResult
            {
                Success = false,
                Duration = sw.Elapsed,
                Message = $"All {applicable.Count} applicable strategies failed for {DisplayName}.\n" +
                          $"Attempt log: {string.Join(" | ", attemptLog)}"
            };
        }

        public virtual TweakResult Verify()
        {
            // Verify checks ALL applicable strategies, not just the one that
            // was originally used — this is what catches a Windows Update
            // reintroducing a feature via a different mechanism than the
            // one ZeroBloat originally disabled it through.
            var applicable = ApplicableStrategies.ToList();
            var driftedStrategies = new List<string>();

            foreach (var strategy in applicable)
            {
                var result = strategy.Verify();
                if (!result.Success)
                    driftedStrategies.Add(strategy.StrategyName);
            }

            bool allGood = driftedStrategies.Count == 0;

            return new TweakResult
            {
                Success = allGood,
                Message = allGood
                    ? $"{DisplayName} verified — all {applicable.Count} applicable strategies still enforced."
                    : $"{DisplayName} DRIFTED — {driftedStrategies.Count} of {applicable.Count} " +
                      $"strategies no longer enforced: {string.Join(", ", driftedStrategies)}."
            };
        }

        public virtual TweakResult Revert()
        {
            var sw = Stopwatch.StartNew();
            var usedStrategyName = MultiStrategyUndoLog.GetUsedStrategy(Id);

            var strategy = usedStrategyName != null
                ? Strategies.FirstOrDefault(s => s.StrategyName == usedStrategyName)
                : null;

            // Fall back to reverting every applicable strategy if we don't
            // know which one was actually used (e.g. undo log was cleared,
            // or Verify() found drift across multiple strategies).
            var toRevert = strategy != null
                ? new List<ITweakStrategy> { strategy }
                : ApplicableStrategies.ToList();

            var attemptLog = new List<string>();
            bool anySuccess = false;

            foreach (var s in toRevert)
            {
                var result = s.Revert();
                attemptLog.Add($"{s.StrategyName}: {(result.Success ? "OK" : "failed")}");
                anySuccess |= result.Success;
            }

            sw.Stop();
            if (anySuccess)
                MultiStrategyUndoLog.ClearUsedStrategy(Id);

            return new TweakResult
            {
                Success = anySuccess,
                Duration = sw.Elapsed,
                Message = anySuccess
                    ? $"Reverted {DisplayName}.\nAttempt log: {string.Join(" | ", attemptLog)}"
                    : $"Failed to revert {DisplayName} — no strategy succeeded.\nAttempt log: {string.Join(" | ", attemptLog)}"
            };
        }

        public virtual TweakCompatibility CheckCompatibility()
        {
            return ApplicableStrategies.Any()
                ? TweakCompatibility.Supported
                : TweakCompatibility.Unsupported;
        }
    }
}