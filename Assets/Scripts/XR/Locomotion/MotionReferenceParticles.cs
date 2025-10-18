using UnityEngine;

namespace Thrustslinger.XR
{
    /// <summary>
    /// Spawns stationary dust-like particles across the player's locomotion plane to provide
    /// visual motion reference. Particles are distributed within the plane bounds defined by
    /// PlayerPlaneDefinition to help players perceive their movement through the space.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerPlaneDefinition))]
    public class MotionReferenceParticles : MonoBehaviour
    {
        [Header("Particle Distribution")]
        [Tooltip("Number of particles to spawn in the play area.")]
        [SerializeField] private int particleCount = 200;

        [Tooltip("Depth range (distance along plane normal) where particles spawn. X=min, Y=max from plane.")]
        [SerializeField] private Vector2 depthRange = new Vector2(0.5f, 10f);

        [Tooltip("Margin from plane edges (in meters) to avoid spawning particles right at boundaries.")]
        [SerializeField] private float edgeMargin = 0.5f;

        [Header("Particle Appearance")]
        [Tooltip("Prefab to instantiate for each particle. Should be a simple quad or sprite.")]
        [SerializeField] private GameObject particlePrefab;

        [Tooltip("Size variation range for particles (min, max in meters).")]
        [SerializeField] private Vector2 sizeRange = new Vector2(0.02f, 0.05f);

        [Tooltip("Alpha variation range for particle transparency.")]
        [SerializeField] private Vector2 alphaRange = new Vector2(0.3f, 0.8f);

        [Header("Runtime Control")]
        [Tooltip("Regenerate particles when plane dimensions change.")]
        [SerializeField] private bool autoUpdateOnPlaneChange = true;

        [Tooltip("Parent transform to hold all particle instances (created automatically if null).")]
        [SerializeField] private Transform particleContainer;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        private PlayerPlaneDefinition _planeDefinition;
        private Vector2 _lastHalfExtents;
        private Vector2 _lastCenterOffset;
        private GameObject[] _spawnedParticles;

        private void Awake()
        {
            _planeDefinition = GetComponent<PlayerPlaneDefinition>();

            if (particleContainer == null)
            {
                var container = new GameObject("MotionReferenceParticles");
                container.transform.SetParent(transform);
                container.transform.localPosition = Vector3.zero;
                container.transform.localRotation = Quaternion.identity;
                particleContainer = container.transform;
            }
        }

        private void Start()
        {
            if (particlePrefab == null)
            {
                Debug.LogWarning($"[MotionReferenceParticles] No particle prefab assigned on {gameObject.name}. Creating default quad.");
                CreateDefaultParticlePrefab();
            }

            if (showDebugInfo)
            {
                Debug.Log($"[MotionReferenceParticles] Starting particle generation. Prefab: {particlePrefab?.name ?? "null"}");
            }

            GenerateParticles();
            _lastHalfExtents = _planeDefinition.HalfExtents;
            _lastCenterOffset = _planeDefinition.CenterOffset;
        }

        private void Update()
        {
            if (autoUpdateOnPlaneChange)
            {
                // Check if plane dimensions changed
                if (_planeDefinition.HalfExtents != _lastHalfExtents || 
                    _planeDefinition.CenterOffset != _lastCenterOffset)
                {
                    if (showDebugInfo)
                    {
                        Debug.Log("[MotionReferenceParticles] Plane dimensions changed, regenerating particles.");
                    }
                    RegenerateParticles();
                    _lastHalfExtents = _planeDefinition.HalfExtents;
                    _lastCenterOffset = _planeDefinition.CenterOffset;
                }
            }
        }

        /// <summary>
        /// Public method to manually trigger particle regeneration.
        /// </summary>
        [ContextMenu("Regenerate Particles")]
        public void RegenerateParticles()
        {
            ClearParticles();
            GenerateParticles();
        }

        private void GenerateParticles()
        {
            if (particlePrefab == null)
            {
                Debug.LogError("[MotionReferenceParticles] Cannot generate particles without a prefab.");
                return;
            }

            _spawnedParticles = new GameObject[particleCount];
            var halfExtents = _planeDefinition.HalfExtents;
            var centerOffset = _planeDefinition.CenterOffset;
            var planePoint = _planeDefinition.PlanePoint;
            var normal = _planeDefinition.Normal;

            // Get plane axes for positioning
            GetPlaneAxes(normal, out var axisX, out var axisY);

            // Calculate effective spawn area with margins
            var effectiveHalfX = Mathf.Max(0.1f, halfExtents.x - edgeMargin);
            var effectiveHalfY = Mathf.Max(0.1f, halfExtents.y - edgeMargin);

            for (int i = 0; i < particleCount; i++)
            {
                // Random position within plane bounds
                var randomX = Random.Range(-effectiveHalfX, effectiveHalfX);
                var randomY = Random.Range(-effectiveHalfY, effectiveHalfY);
                var randomDepth = Random.Range(depthRange.x, depthRange.y);

                // Calculate world position
                var planeCenter = planePoint + axisX * centerOffset.x + axisY * centerOffset.y;
                var positionOnPlane = planeCenter + axisX * randomX + axisY * randomY;
                var worldPosition = positionOnPlane + normal * randomDepth;

                // Spawn particle
                var particle = Instantiate(particlePrefab, worldPosition, Quaternion.identity, particleContainer);
                particle.SetActive(true); // Ensure particle is active
                
                // Orient particle to always face opposite of plane normal (towards player)
                particle.transform.rotation = Quaternion.LookRotation(-normal);

                // Randomize size
                var size = Random.Range(sizeRange.x, sizeRange.y);
                particle.transform.localScale = Vector3.one * size;

                // Randomize alpha if particle has a renderer
                var renderer = particle.GetComponent<Renderer>();
                if (renderer != null && renderer.material != null)
                {
                    var mat = renderer.material;
                    var color = mat.color;
                    color.a = Random.Range(alphaRange.x, alphaRange.y);
                    mat.color = color;

                    if (showDebugInfo && i == 0)
                    {
                        Debug.Log($"[MotionReferenceParticles] First particle - Material: {mat.name}, Shader: {mat.shader.name}, Color: {color}");
                    }
                }

                _spawnedParticles[i] = particle;
            }

            if (showDebugInfo)
            {
                Debug.Log($"[MotionReferenceParticles] Generated {particleCount} particles across plane bounds " +
                          $"({halfExtents.x * 2}x{halfExtents.y * 2}m) at depths {depthRange.x}-{depthRange.y}m");
            }
        }

        private void ClearParticles()
        {
            if (_spawnedParticles != null)
            {
                foreach (var particle in _spawnedParticles)
                {
                    if (particle != null)
                    {
                        Destroy(particle);
                    }
                }
                _spawnedParticles = null;
            }

            // Fallback: clear any remaining children
            if (particleContainer != null)
            {
                for (int i = particleContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(particleContainer.GetChild(i).gameObject);
                }
            }
        }

        private void GetPlaneAxes(Vector3 normal, out Vector3 axisX, out Vector3 axisY)
        {
            // Use plane definition's reference transform if available
            var refRight = _planeDefinition.reference ? _planeDefinition.reference.right : Vector3.right;
            var xProj = Vector3.ProjectOnPlane(refRight, normal);
            
            if (xProj.sqrMagnitude < 1e-6f)
            {
                // Fallback if reference right is parallel to normal
                var arbitrary = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.99f ? Vector3.up : Vector3.forward;
                xProj = Vector3.ProjectOnPlane(arbitrary, normal);
            }
            
            axisX = xProj.normalized;
            axisY = Vector3.Cross(normal, axisX).normalized;
        }

        private void CreateDefaultParticlePrefab()
        {
            // Create a simple quad mesh for default particles
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "DefaultDustParticle";
            
            // Remove collider
            var collider = quad.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            // Try to find a transparent shader - fallback chain
            Shader shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                Debug.LogWarning("[MotionReferenceParticles] No transparent shader found, using Standard shader.");
                shader = Shader.Find("Standard");
            }

            // Create material with transparency
            var material = new Material(shader);
            material.color = new Color(0.8f, 0.8f, 0.8f, 0.5f);
            
            // Enable transparency for Standard shader if that's what we're using
            if (shader.name.Contains("Standard"))
            {
                material.SetFloat("_Mode", 3); // Transparent mode
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
            }
            
            quad.GetComponent<Renderer>().material = material;

            // Hide from hierarchy but keep enabled
            quad.hideFlags = HideFlags.HideAndDontSave;
            particlePrefab = quad;

            if (showDebugInfo)
            {
                Debug.Log($"[MotionReferenceParticles] Created default particle prefab with shader: {shader.name}");
            }
        }

        private void OnDestroy()
        {
            ClearParticles();
        }

        private void OnValidate()
        {
            particleCount = Mathf.Max(1, particleCount);
            depthRange.x = Mathf.Max(0f, depthRange.x);
            depthRange.y = Mathf.Max(depthRange.x + 0.1f, depthRange.y);
            edgeMargin = Mathf.Max(0f, edgeMargin);
            sizeRange.x = Mathf.Max(0.001f, sizeRange.x);
            sizeRange.y = Mathf.Max(sizeRange.x, sizeRange.y);
            alphaRange.x = Mathf.Clamp01(alphaRange.x);
            alphaRange.y = Mathf.Clamp(alphaRange.x, alphaRange.y, 1f);
        }
    }
}
