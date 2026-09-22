using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AlreadyDead.Tests
{
    public sealed class FantasyPlayModeTests
    {
        private TopDownPlayer player;
        private PrototypeTuning tuning;
        private FantasyEnemy enemy;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            player = Object.FindAnyObjectByType<TopDownPlayer>();
            Assert.That(player, Is.Not.Null);
            player.enabled = false;
            player.Body.linearVelocity = Vector2.zero;
            tuning = Object.Instantiate(player.Tuning);
            tuning.wallMask = 1 << 8;
            player.Body.position = new Vector2(73f, 70f);
            player.transform.position = player.Body.position;
            Physics2D.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (enemy != null) Object.Destroy(enemy.gameObject);
            if (tuning != null) Object.Destroy(tuning);
            yield return null;
        }

        [Test]
        public void ElementsDamageAndControlFantasyEnemies()
        {
            enemy = CreateEnemy(FantasyEnemyKind.Knight, new Vector2(70f, 70f));
            enemy.enabled = false;
            int start = enemy.Health;

            enemy.TakeMagicDamage(1, MagicElement.Frost, Vector2.right);
            Assert.That(enemy.Health, Is.EqualTo(start - 1));
            Assert.That(enemy.SpeedMultiplier, Is.LessThan(0.5f), "Frost slows a knight");
            Assert.That(enemy.Alerted, Is.True, "Taking damage alerts an enemy");

            enemy.TakeMagicDamage(2, MagicElement.Fire, Vector2.right);
            Assert.That(enemy.Health, Is.EqualTo(start - 3));
            enemy.TakeMagicDamage(2, MagicElement.Lightning, Vector2.right);
            Assert.That(enemy.IsAlive, Is.False);
            Assert.That(enemy.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator KnightAttacksInMeleeAndMageFiresAtRange()
        {
            player.Body.position = new Vector2(70.75f, 70f);
            player.transform.position = player.Body.position;
            enemy = CreateEnemy(FantasyEnemyKind.Knight, new Vector2(70f, 70f));
            Physics2D.SyncTransforms();
            int initialHealth = player.Vitality.Health;
            yield return new WaitForSeconds(0.17f);
            Assert.That(enemy.Alerted, Is.True);
            Assert.That(enemy.AttacksMade, Is.EqualTo(1));
            Assert.That(player.Vitality.Health, Is.EqualTo(initialHealth - 1));

            Object.Destroy(enemy.gameObject);
            enemy = null;
            yield return new WaitForSeconds(0.6f);
            player.Body.position = new Vector2(73f, 70f);
            player.transform.position = player.Body.position;
            enemy = CreateEnemy(FantasyEnemyKind.Mage, new Vector2(70f, 70f));
            Physics2D.SyncTransforms();
            int beforeBolt = player.Vitality.Health;
            yield return new WaitForSeconds(0.1f);
            Assert.That(enemy.AttacksMade, Is.EqualTo(1));
            Assert.That(Object.FindAnyObjectByType<FantasyEnemyBolt>(), Is.Not.Null,
                "Mage casts a visible traveling bolt");
            yield return new WaitForSeconds(0.48f);
            Assert.That(player.Vitality.Health, Is.EqualTo(beforeBolt - 1));
        }

        [UnityTest]
        public IEnumerator CoverStopsMageVisionAndBolts()
        {
            var wall = new GameObject("Fantasy test cover");
            wall.layer = 8;
            wall.transform.position = new Vector2(71.5f, 70f);
            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.3f, 2f);
            enemy = CreateEnemy(FantasyEnemyKind.Mage, new Vector2(70f, 70f));
            Physics2D.SyncTransforms();
            Assert.That(enemy.CanSeePlayer(), Is.False);
            yield return new WaitForSeconds(0.2f);
            Assert.That(enemy.AttacksMade, Is.Zero);

            // Once alerted from damage, the mage can still pursue, but a bolt stops at cover.
            enemy.TakeMagicDamage(1, MagicElement.Fire, Vector2.right);
            int initialHealth = player.Vitality.Health;
            FantasyEnemyBolt.Spawn(new Vector2(70f, 70f), Vector2.right,
                tuning, player, MagicElement.Fire, null, null);
            yield return new WaitForSeconds(0.5f);
            Assert.That(player.Vitality.Health, Is.EqualTo(initialHealth));
            Object.Destroy(wall);
        }

        [UnityTest]
        public IEnumerator FantasySceneContainsOneUniversalStaffAndEnemyClasses()
        {
            yield return SceneManager.LoadSceneAsync("FantasyScene");
            TopDownPlayer fantasyPlayer = Object.FindAnyObjectByType<TopDownPlayer>();
            Assert.That(fantasyPlayer, Is.Not.Null);
            Assert.That(fantasyPlayer.IsAlive, Is.True);
            MagicStaff[] staffs = Object.FindObjectsByType<MagicStaff>(FindObjectsSortMode.None);
            Assert.That(staffs.Length, Is.EqualTo(1), "Old separate elemental staffs were removed");
            foreach (MagicElement element in new[] { MagicElement.Fire, MagicElement.Frost, MagicElement.Lightning })
            {
                staffs[0].SelectElement(element);
                Assert.That(staffs[0].SelectedElement, Is.EqualTo(element));
            }
            FantasyEnemy[] enemies = Object.FindObjectsByType<FantasyEnemy>(FindObjectsSortMode.None);
            Assert.That(System.Array.Exists(enemies, foe => foe.Kind == FantasyEnemyKind.Knight), Is.True);
            Assert.That(System.Array.Exists(enemies, foe => foe.Kind == FantasyEnemyKind.Mage), Is.True);
        }

        [UnityTest]
        public IEnumerator DesertGateLoadsTheForest()
        {
            FantasyLevelGate gate = System.Array.Find(
                Object.FindObjectsByType<FantasyLevelGate>(FindObjectsSortMode.None),
                candidate => candidate.DestinationScene == "FantasyScene");
            Assert.That(gate, Is.Not.Null);
            player.Body.position = gate.transform.position;
            player.transform.position = gate.transform.position;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("FantasyScene"));
        }

        [UnityTest]
        public IEnumerator PlayerCanEquipAndSwitchTheUniversalStaff()
        {
            yield return SceneManager.LoadSceneAsync("FantasyScene");
            TopDownPlayer mage = Object.FindAnyObjectByType<TopDownPlayer>();
            mage.enabled = false;
            MagicStaff[] staffs = Object.FindObjectsByType<MagicStaff>(FindObjectsSortMode.None);
            Assert.That(staffs.Length, Is.EqualTo(1));
            MagicStaff universal = staffs[0];
            FantasyEnemy knight = System.Array.Find(
                Object.FindObjectsByType<FantasyEnemy>(FindObjectsSortMode.None),
                foe => foe.Kind == FantasyEnemyKind.Knight);
            Assert.That(universal, Is.Not.Null);
            Assert.That(knight, Is.Not.Null);

            Assert.That(mage.Interact(universal.transform.position), Is.True);
            Assert.That(mage.HeldStaff, Is.EqualTo(universal));
            knight.enabled = false;
            knight.Body.linearVelocity = Vector2.zero;
            knight.Body.position = new Vector2(3f, -8f);
            knight.transform.position = knight.Body.position;
            Physics2D.SyncTransforms();
            int initialHealth = knight.Health;
            mage.AimAt(new Vector2(3f, -8f));
            Assert.That(mage.TryPrimaryAttack(), Is.True);
            yield return new WaitForSeconds(0.35f);
            Assert.That(universal.CastsMade, Is.EqualTo(1));
            Assert.That(knight.Health, Is.EqualTo(initialHealth - 2), "Fire cast reaches the enemy");

            yield return new WaitForSeconds(mage.Tuning.fireCastInterval + 0.02f);
            universal.SelectElement(MagicElement.Frost);
            Assert.That(mage.TryPrimaryAttack(), Is.True);
            Assert.That(universal.LastVolleyCount, Is.InRange(4, 6));
            universal.SelectElement(MagicElement.Lightning);
            Assert.That(universal.SelectedElement, Is.EqualTo(MagicElement.Lightning));
        }

        private FantasyEnemy CreateEnemy(FantasyEnemyKind kind, Vector2 position)
        {
            var root = new GameObject("Fantasy test / " + kind);
            root.layer = 11;
            root.transform.position = position;
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            var hitbox = root.AddComponent<CircleCollider2D>();
            hitbox.radius = 0.32f;
            var visual = new GameObject("Facing").transform;
            visual.SetParent(root.transform, false);
            var indicator = new GameObject("Alert").AddComponent<SpriteRenderer>();
            indicator.transform.SetParent(root.transform, false);
            var created = root.AddComponent<FantasyEnemy>();
            created.Configure(tuning, player, visual, indicator, position,
                position + Vector2.right, kind, MagicElement.Fire);
            return created;
        }
    }
}
