using UnityEngine;

namespace HandShooter
{
    [RequireComponent(typeof(Collider), typeof(Rigidbody))]
    public sealed class FuelTankPickup : MonoBehaviour
    {
        private static Material tankMaterial;
        private static Material stripeMaterial;
        [Min(1)] public int tanksGranted = 1;
        public Transform visualRoot;
        [Min(0f)] public float hoverAmplitude = 0.2f;
        [Min(0.01f)] public float hoverFrequency = 1.5f;
        [Min(0f)] public float rotationSpeed = 70f;
        internal bool IsCollected { get; private set; }

        private void Awake()
        {
            var trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        private void Update()
        {
            if (!visualRoot) return;
            visualRoot.localPosition = Vector3.up * (Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude);
            visualRoot.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        }

        public static void SpawnDefaultPickups(Transform player)
        {
            if (!player || FindObjectsByType<FuelTankPickup>(FindObjectsSortMode.None).Length > 0) return;
            if (!tankMaterial)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) shader = Shader.Find("Unlit/Color");
                tankMaterial = new Material(shader); tankMaterial.color = new Color(1f, 0.48f, 0.12f);
                stripeMaterial = new Material(shader); stripeMaterial.color = new Color(0.25f, 0.94f, 0.72f);
            }
            Vector3[] offsets = { player.forward * 3.5f + player.right * 3f, player.forward * 11f - player.right * 6f, player.forward * 19f + player.right * 4f };
            for (int i = 0; i < offsets.Length; i++)
            {
                var pickup = new GameObject($"Fuel Tank {i + 1:00}");
                pickup.transform.position = player.position + offsets[i] + Vector3.up * 0.9f;
                var trigger = pickup.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.radius = 0.7f;
                var body = pickup.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                var fuel = pickup.AddComponent<FuelTankPickup>(); fuel.tanksGranted = 1;
                var visuals = new GameObject("Visuals").transform; visuals.SetParent(pickup.transform, false); fuel.visualRoot = visuals;
                CreateVisual(PrimitiveType.Cylinder, "Canister", visuals, new Vector3(0.38f, 0.38f, 0.38f), Vector3.zero, tankMaterial);
                CreateVisual(PrimitiveType.Cube, "Fuel stripe", visuals, new Vector3(0.4f, 0.1f, 0.4f), new Vector3(0f, 0.05f, 0f), stripeMaterial);
            }
        }

        private static void CreateVisual(PrimitiveType type, string name, Transform parent, Vector3 scale, Vector3 position, Material material)
        {
            var visual = GameObject.CreatePrimitive(type);
            visual.name = name; visual.transform.SetParent(parent, false);
            visual.transform.localScale = scale; visual.transform.localPosition = position;
            visual.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(visual.GetComponent<Collider>());
        }

        private void OnTriggerEnter(Collider other)
        {
            PrototypePlayer player = other.GetComponentInParent<PrototypePlayer>();
            if (IsCollected || !player || !player.weapon || player.IsDead) return;
            IsCollected = true;
            player.weapon.AddFuelTank(tanksGranted);
            Destroy(gameObject);
        }
    }
}
