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
}
}