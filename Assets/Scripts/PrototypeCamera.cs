using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class PrototypeCamera : MonoBehaviour
{
    [SerializeField] private PrototypePlayer player;
    [SerializeField] private Vector3 focus = new Vector3(0f, 0.5f, 0f);
    [SerializeField, Range(0f, 89f)] private float gridPitch = 70f;

    [SerializeField] private bool followRows;
    [SerializeField] private float minFocusZ;
    [SerializeField] private float maxFocusZ;

    [SerializeField] private Material stonework;
    [SerializeField] private float gridYaw;
    private Transform scenery;

    private void Start()
    {
        if (stonework == null)
            return;

        // This scenery sits below the playable deck. It has no colliders.
        scenery = new GameObject("Mint Court · scenery").transform;
        var root = scenery;
        root.SetParent(transform.parent, false);
        var rose = new Color(.78f, .59f, .67f);
        var lilac = new Color(.64f, .54f, .71f);
        var ivory = new Color(.94f, .86f, .78f);
        AddStone("Cornice", new Vector3(7, -1.12f, 2), new Vector3(2.96f, .22f, 2.9f), ivory);
        for (int i = 0; i < 2; i++)
        {
            float x = 6.05f + i * 1.9f;
            AddStone("Pier", new Vector3(x, -2.15f, 1.1f), new Vector3(.72f, 1.9f, .84f), rose);
            AddStone("Pier foot", new Vector3(x, -3.12f, 1.1f), new Vector3(.96f, .22f, 1.06f), lilac);
            AddStone("Capital", new Vector3(x, -1.42f, 1.1f), new Vector3(.89f, .19f, 1f), ivory);
        }
        AddStone("Portal lintel", new Vector3(7, -1.51f, 1.1f), new Vector3(2.73f, .32f, .8f), lilac);
        AddStone("Far pier", new Vector3(8, -2.15f, 6.9f), new Vector3(.82f, 2.3f, .82f), lilac);
        for (int i = 0; i < 3; i++)
            AddStone("Court plinth", new Vector3(11.1f, -2.8f + i * .24f, 5.9f),
                new Vector3(1.85f - i * .38f, .24f, 1.85f - i * .38f), i == 1 ? rose : ivory);
        AddStone("Court pillar", new Vector3(11.1f, -1.7f, 5.9f), new Vector3(.62f, 1.55f, .62f), lilac);
        AddStone("Court crown", new Vector3(11.1f, -.84f, 5.9f), new Vector3(.91f, .19f, .91f), ivory);

        void AddStone(string label, Vector3 position, Vector3 scale, Color tint)
        {
            GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stone.name = label;
            stone.transform.SetParent(root, false);
            stone.transform.SetPositionAndRotation(position, Quaternion.identity);
            stone.transform.localScale = scale;
            Collider shape = stone.GetComponent<Collider>();
            shape.enabled = false;
            Destroy(shape);
            var renderer = stone.GetComponent<Renderer>();
            renderer.sharedMaterial = stonework;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", tint);
            renderer.SetPropertyBlock(properties);
        }
    }

    private void LateUpdate()
    {
        bool platform = player.Mode == PlayerMode.Platform;
        if (scenery != null && scenery.gameObject.activeSelf == platform)
            scenery.gameObject.SetActive(!platform);
        Quaternion rotation = Quaternion.Euler(platform ? 90f : gridPitch, platform ? 0f : gridYaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -16f);
        Vector3 target = focus;
        if (followRows)
            target.z = Mathf.Clamp(player.transform.position.z, minFocusZ, maxFocusZ);
        transform.SetPositionAndRotation(target + offset, rotation);
    }
}
