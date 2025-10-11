using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Thrustslinger.Core
{
    /// <summary>
    /// Lightweight runtime score aggregator used during prototype development.
    /// Implements <see cref="IRunScoreService"/> so it can plug directly into the <see cref="GameManager"/>.
    /// </summary>
    [AddComponentMenu("Thrustslinger/Core/Runtime Score Service")]
    [DisallowMultipleComponent]
    public sealed class RuntimeScoreService : MonoBehaviour, IRunScoreService, IRuntimeScoreProvider
    {
        [Header("Scoring")]
        [Tooltip("Base points granted per kill when the data does not specify a score.")]
        [SerializeField, Min(0f)] private float baseKillScore = 100f;
        [Tooltip("Multiplier applied for dead-center hits.")]
        [SerializeField, Min(0f)] private float centerMultiplier = 2.0f;
        [Tooltip("Multiplier applied for near-center hits.")]
        [SerializeField, Min(0f)] private float nearCenterMultiplier = 1.4f;
        [Tooltip("Multiplier applied for body hits.")]
        [SerializeField, Min(0f)] private float bodyMultiplier = 1.0f;
        [Tooltip("Multiplier applied for graze hits.")]
        [SerializeField, Min(0f)] private float grazeMultiplier = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool logKillEvents;
        [SerializeField] private bool logSubmissions;

        [Header("High Score Persistence")]
        [SerializeField] private bool trackHighScore = true;
        [SerializeField] private string highScorePrefsKey = "Thrustslinger_HighScore";

        private readonly List<RunKillData> _killLog = new();

        public event Action<float> ScoreChanged;

        public float CurrentScore { get; private set; }
        public int KillCount => _killLog.Count;
        public float HighScore { get; private set; }

        private void Awake()
        {
            LoadHighScore();
        }

        public void ResetScore()
        {
            CurrentScore = 0f;
            _killLog.Clear();
            ScoreChanged?.Invoke(CurrentScore);
        }

        public void BeginRun(RunContext context)
        {
            ResetScore();
        }

        public void RegisterKill(in RunKillData killData)
        {
            var awarded = killData.scoreAwarded;
            if (awarded <= 0f)
            {
                awarded = baseKillScore * GetAccuracyMultiplier(killData.accuracy);
            }

            CurrentScore += Mathf.Max(0f, awarded);

            var logged = killData;
            logged.scoreAwarded = awarded;
            _killLog.Add(logged);

            if (logKillEvents)
            {
                Debug.Log($"[RuntimeScoreService] Kill registered: archetype={logged.archetypeId} accuracy={logged.accuracy} score={logged.scoreAwarded:F1} total={CurrentScore:F1}");
            }

            ScoreChanged?.Invoke(CurrentScore);
        }

        public RunSummary BuildSummary()
        {
            var summary = new RunSummary
            {
                finalScore = CurrentScore,
                targetsDestroyed = KillCount,
                accuracy = ComputeAverageAccuracy(),
                killLog = new List<RunKillData>(_killLog)
            };

            return summary;
        }

        public void FinalizeRun(RunSummary summary)
        {
            if (summary == null)
            {
                return;
            }

            summary.finalScore = CurrentScore;
            summary.targetsDestroyed = KillCount;
            summary.accuracy = ComputeAverageAccuracy();
            summary.killLog = new List<RunKillData>(_killLog);

            // Check and update high score
            if (trackHighScore && CurrentScore > HighScore)
            {
                HighScore = CurrentScore;
                SaveHighScore();
            }
        }

        public void SubmitResults(RunSummary summary)
        {
            if (logSubmissions)
            {
                var status = summary != null
                    ? $"score={summary.finalScore:F0} kills={summary.targetsDestroyed} accuracy={summary.accuracy:P0}"
                    : $"score={CurrentScore:F0} kills={KillCount}";
                Debug.Log($"[RuntimeScoreService] SubmitResults -> {status}");
            }
        }

        private void LoadHighScore()
        {
            if (!trackHighScore)
            {
                HighScore = 0f;
                return;
            }

            HighScore = PlayerPrefs.GetFloat(highScorePrefsKey, 0f);
        }

        private void SaveHighScore()
        {
            if (!trackHighScore)
            {
                return;
            }

            PlayerPrefs.SetFloat(highScorePrefsKey, HighScore);
            PlayerPrefs.Save();
        }

        private float GetAccuracyMultiplier(HitAccuracyBucket accuracy)
        {
            return accuracy switch
            {
                HitAccuracyBucket.Center => centerMultiplier,
                HitAccuracyBucket.NearCenter => nearCenterMultiplier,
                HitAccuracyBucket.Body => bodyMultiplier,
                _ => grazeMultiplier
            };
        }

        private float ComputeAverageAccuracy()
        {
            if (_killLog.Count == 0)
            {
                return 0f;
            }

            var total = _killLog.Sum(k => AccuracyToScalar(k.accuracy));
            return Mathf.Clamp01(total / _killLog.Count);
        }

        private static float AccuracyToScalar(HitAccuracyBucket bucket)
        {
            return bucket switch
            {
                HitAccuracyBucket.Center => 1.0f,
                HitAccuracyBucket.NearCenter => 0.85f,
                HitAccuracyBucket.Body => 0.6f,
                HitAccuracyBucket.Graze => 0.35f,
                _ => 0.5f
            };
        }

#if UNITY_EDITOR
        [ContextMenu("Clear High Score")]
        private void ClearHighScore()
        {
            if (!trackHighScore)
            {
                Debug.LogWarning("[RuntimeScoreService] High score tracking is disabled.", this);
                return;
            }

            if (PlayerPrefs.HasKey(highScorePrefsKey))
            {
                PlayerPrefs.DeleteKey(highScorePrefsKey);
                PlayerPrefs.Save();
                HighScore = 0f;
                Debug.Log($"[RuntimeScoreService] High score cleared from PlayerPrefs key: {highScorePrefsKey}", this);
            }
            else
            {
                Debug.Log("[RuntimeScoreService] No high score found to clear.", this);
            }
        }
#endif
    }
}
