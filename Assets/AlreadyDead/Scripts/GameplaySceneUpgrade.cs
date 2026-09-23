using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlreadyDead
{
    // Also upgrades checked-in scenes when the editor scene builder has not been run yet.
    internal static class GameplaySceneUpgrade
    {
        private static Sprite projectileSprite;
        private static Material projectileMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Upgrade(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Upgrade(scene);

        private static void Upgrade(Scene scene)
        {
            if (scene.name != "SampleScene" && scene.name != "FantasyScene") return;
            TopDownPlayer player = Object.FindAnyObjectByType<TopDownPlayer>();
            if (player == null) return;
            InstallOutline(player.Facing);
            foreach (PatrolEnemy enemy in Object.FindObjectsByType<PatrolEnemy>())
                InstallOutline(FindFacing(enemy.transform));
            foreach (FantasyEnemy enemy in Object.FindObjectsByType<FantasyEnemy>())
                InstallOutline(FindFacing(enemy.transform));

            if (scene.name != "SampleScene") return;
            MusketWeapon existingMusket = Object.FindAnyObjectByType<MusketWeapon>();
            if (existingMusket != null && Mathf.Approximately(existingMusket.transform.localScale.x, 1.6f))
                existingMusket.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            Vector2 origin = player.transform.position;
            if (GameObject.Find("Glock / 17 rounds") == null)
                CreateGun("Glock / 17 rounds", origin + Vector2.up * 1.25f,
                    player.Tuning, false, 17);
            GameObject m4 = GameObject.Find("M4 / 25 rounds automatic");
            if (m4 == null)
                CreateGun("M4 / 25 rounds automatic", origin + new Vector2(1.65f, 1.25f),
                    player.Tuning, true, 25);
            else if (Mathf.Approximately(m4.transform.localScale.x, 1f))
                m4.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
        }

        private static Transform FindFacing(Transform root)
        {
            foreach (Transform child in root)
                if (child.name.StartsWith("Facing")) return child;
            return null;
        }

        private static void InstallOutline(Transform facing)
        {
            if (facing == null) return;
            foreach (SpriteRenderer renderer in facing.GetComponentsInChildren<SpriteRenderer>(true))
            {
                string part = renderer.name.ToLowerInvariant();
                if (part.StartsWith("black pixel outline") ||
                    !(part.Contains("body") || part.Contains("arm") || part.Contains("leg") ||
                      part.Contains("mage") || part.Contains("knight"))) continue;
                Transform source = renderer.transform;
                int copies = 0;
                foreach (Transform child in source)
                {
                    if (!child.name.StartsWith("Black pixel outline")) continue;
                    SpriteRenderer outline = child.GetComponent<SpriteRenderer>();
                    if (outline == null) continue;
                    outline.sprite = renderer.sprite;
                    outline.sharedMaterial = renderer.sharedMaterial;
                    outline.sortingOrder = renderer.sortingOrder - 1;
                    copies++;
                }
                if (copies == 0)
                {
                    for (int i = 0; i < 32; i++)
                    {
                        var go = new GameObject("Black pixel outline " + i);
                        go.transform.SetParent(source, false);
                        SpriteRenderer outline = go.AddComponent<SpriteRenderer>();
                        outline.sprite = renderer.sprite;
                        outline.sharedMaterial = renderer.sharedMaterial;
                        outline.color = new Color(0.025f, 0.018f, 0.016f, 1f);
                        outline.sortingLayerID = renderer.sortingLayerID;
                        outline.sortingOrder = renderer.sortingOrder - 1;
                        outline.flipX = renderer.flipX;
                        outline.flipY = renderer.flipY;
                    }
                }
                if (source.GetComponent<ScreenPixelOutline>() == null)
                    source.gameObject.AddComponent<ScreenPixelOutline>();
            }
        }

        private static void CreateGun(string name, Vector2 position, PrototypeTuning tuning,
            bool automatic, int capacity)
        {
            string prefix = automatic ? "M4" : "Glock";
            Texture2D sideTexture = Resources.Load<Texture2D>("WeaponVariants/" + prefix + "_Side");
            Texture2D topTexture = Resources.Load<Texture2D>("WeaponVariants/" + prefix + "_UP");
            if (sideTexture == null || topTexture == null)
            {
                Debug.LogError("Missing weapon variants for " + prefix);
                return;
            }
            Sprite sideSprite = Sprite.Create(sideTexture,
                new Rect(0f, 0f, sideTexture.width, sideTexture.height),
                new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
            Sprite topSprite = Sprite.Create(topTexture,
                new Rect(0f, 0f, topTexture.width, topTexture.height),
                new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
            if (projectileSprite == null)
                projectileSprite = Sprite.Create(Texture2D.whiteTexture,
                    new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                    new Vector2(0.5f, 0.5f), 2f, 0u, SpriteMeshType.FullRect);
            if (projectileMaterial == null)
                projectileMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));

            var go = new GameObject(name);
            go.SetActive(false);
            go.layer = 9;
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, 25f);
            if (automatic) go.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.mass = automatic ? 1.2f : 0.75f;
            body.linearDamping = tuning.throwLinearDamping;
            body.angularDamping = tuning.throwAngularDamping;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = automatic ? new Vector2(1.55f, 0.42f) : new Vector2(0.85f, 0.5f);
            MusketWeapon musket = Object.FindAnyObjectByType<MusketWeapon>();
            if (musket != null) collider.sharedMaterial = musket.Hitbox.sharedMaterial;
            else collider.sharedMaterial = new PhysicsMaterial2D("Gun bounce");

            Transform visual = new GameObject("Visual / recoil").transform;
            visual.SetParent(go.transform, false);
            float scale = automatic ? 0.16f : 0.18f;
            SpriteRenderer ground = MakeRenderer(prefix + " side / ground", visual, sideSprite, scale, 15);
            SpriteRenderer held = MakeRenderer(prefix + " top / held", visual, topSprite, scale, 15);
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(visual, false);
            muzzle.localPosition = new Vector3(automatic ? 0.8f : 0.43f, 0f, 0f);
            SpriteRenderer flash = MakeRenderer("Muzzle flash", muzzle, projectileSprite, 1f, 18);
            flash.transform.localPosition = new Vector3(0.14f, 0f, 0f);
            flash.transform.localScale = new Vector3(0.36f, 0.22f, 1f);
            flash.color = new Color(1f, 0.9f, 0.55f);

            PistolWeapon gun = go.AddComponent<PistolWeapon>();
            gun.Configure(tuning, visual, muzzle, null, flash, projectileSprite, projectileMaterial);
            gun.ConfigureFirearm(capacity, automatic, ground, held);
            go.SetActive(true);
        }

        private static SpriteRenderer MakeRenderer(string name, Transform parent, Sprite sprite,
            float scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
