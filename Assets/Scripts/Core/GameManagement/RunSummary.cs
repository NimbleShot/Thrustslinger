using System;
using System.Collections.Generic;
using UnityEngine;

namespace Thrustslinger.Core
{
    /// <summary>
    /// Snapshot of the outcome for a single arena run. Built when the game transitions to Results.
    /// </summary>
    [Serializable]
    public class RunSummary
    {
        [Tooltip("Context that produced this run (mode, comfort, difficulty).")]
        public RunContext context;

        [Tooltip("Total unpaused run length in seconds.")]
        public float runTimeSeconds;

        [Tooltip("Seconds that contributed to difficulty ramp after the warmup.")]
        public float difficultyTimeSeconds;

        [Tooltip("Finalised score reported by the score service.")]
        public float finalScore;

        [Tooltip("Number of targets destroyed during the run.")]
        public int targetsDestroyed;

        [Tooltip("Number of breaches that reached the player plane.")]
        public int breaches;

        [Tooltip("Overall hit accuracy ratio (0..1).")]
        [Range(0f, 1f)]
        public float accuracy;

        [Tooltip("Optional detailed kill breakdown for analytics and UI effects.")]
        public List<RunKillData> killLog = new();

        public RunSummary Clone()
        {
            var clone = (RunSummary)MemberwiseClone();
            clone.context = context?.Clone();
            clone.killLog = killLog != null ? new List<RunKillData>(killLog) : new List<RunKillData>();
            return clone;
        }
    }

    /// <summary>
    /// Lightweight description of a single kill used for scoring / analytics.
    /// </summary>
    [Serializable]
    public struct RunKillData
    {
        public string archetypeId;
        public float distanceMeters;
        public HitAccuracyBucket accuracy;
        public float scoreAwarded;

        public RunKillData(string archetypeId, float distanceMeters, HitAccuracyBucket accuracy, float scoreAwarded)
        {
            this.archetypeId = archetypeId;
            this.distanceMeters = distanceMeters;
            this.accuracy = accuracy;
            this.scoreAwarded = scoreAwarded;
        }
    }

    public enum HitAccuracyBucket
    {
        Center = 0,
        NearCenter = 1,
        Body = 2,
        Graze = 3
    }
}
