using UnityEngine;


namespace Thrustslinger.XR
{
[CreateAssetMenu(menuName = "Thrustslinger/ThrusterConfig", fileName = "ThrusterConfig")]
public class ThrusterConfig : ScriptableObject
{
[Header("Force & Damping")]
[Min(0)] public float maxAcceleration = 12f; // m/s^2 per hand at full grip
[Range(0, 1f)] public float damping = 0.05f; // simple linear damping


[Header("Input")]
[Range(0, 0.5f)] public float gripDeadZone = 0.05f;
[Range(0, 60f)] public float palmDeadZoneDegrees = 12f; // ignore if palm nearly parallel to plane


[Header("Curves")]
public AnimationCurve gripToForce = AnimationCurve.EaseInOut(0, 0, 1, 1);


[Header("Gizmos")]
public Color thrustGizmoColor = new Color(1f, 0.6f, 0.2f, 0.8f);

[Header("Audio")]
[Tooltip("AudioClip played when thrusting. Should be seamlessly looping.")]
public AudioClip thrustLoopClip;

[Tooltip("Minimum volume when thrust starts (grip just above dead zone).")]
[Range(0f, 1f)] public float minVolume = 0.1f;

[Tooltip("Maximum volume at full thrust (grip = 1).")]
[Range(0f, 1f)] public float maxVolume = 0.8f;

[Tooltip("Optional curve to shape volume response to grip intensity (x=normalized grip [0..1], y=volume multiplier).")]
public AnimationCurve volumeCurve = AnimationCurve.Linear(0, 0, 1, 1);

[Tooltip("Pitch at minimum thrust.")]
[Range(0.5f, 2f)] public float minPitch = 0.8f;

[Tooltip("Pitch at maximum thrust.")]
[Range(0.5f, 2f)] public float maxPitch = 1.2f;

[Tooltip("Smoothing speed for volume/pitch changes (higher = snappier).")]
[Min(0f)] public float audioSmoothingSpeed = 10f;
}
}