using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AlreadyDead.Tests
{
    public sealed class DesertLevelPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadDesert()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
        }

        [UnityTest]
        public IEnumerator MapHasTheRequestedStartRocksEnemiesAndExit()
        {
            TopDownPlayer player = Object.FindAnyObjectByType<TopDownPlayer>();
            Assert.That(Vector2.Distance(player.transform.position, new Vector2(-9f, -8.15625f)),
                Is.LessThan(0.01f));
            PatrolEnemy[] enemies = Object.FindObjectsByType<PatrolEnemy>();
            Assert.That(enemies.Length, Is.EqualTo(7));
            Transform playerBody = GameObject.Find("Caveman body / sprite forward is up").transform;
            Transform enemyBody = GameObject.Find("Raider body").transform;
            Assert.That(playerBody.localScale.x, Is.EqualTo(0.18f * 1.33f).Within(0.001f));
            Assert.That(enemyBody.localScale.x, Is.EqualTo(0.18f * 1.33f).Within(0.001f));
            Transform details = GameObject.Find("Desert details / stones and dry shrubs").transform;
            Assert.That(details.GetComponentsInChildren<RockWeapon>().Length, Is.EqualTo(9));
            Assert.That(Object.FindObjectsByType<PistolWeapon>().Length, Is.Zero);
            MusketWeapon[] muskets = Object.FindObjectsByType<MusketWeapon>();
            Assert.That(muskets.Length, Is.EqualTo(1));
            Assert.That(muskets[0].transform.localScale.x, Is.EqualTo(1.6f).Within(0.001f));
            Assert.That(muskets[0].transform.localScale.y, Is.EqualTo(1.6f).Within(0.001f));
            Assert.That(Vector2.Distance(muskets[0].transform.position,
                (Vector2)player.transform.position + Vector2.right * 1.2f), Is.LessThan(0.01f));
            Assert.That(Object.FindObjectsByType<MagicStaff>().Length, Is.Zero);

            FantasyLevelGate gate = Object.FindAnyObjectByType<FantasyLevelGate>();
            Assert.That(gate.DestinationScene, Is.EqualTo("FantasyScene"));
            Assert.That(gate.transform.position.x, Is.LessThan(-13f));
            Assert.That(gate.transform.position.y, Is.EqualTo(-1.9f).Within(0.01f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FirstGuardAlwaysDropsAUsableSpear()
        {
            TopDownPlayer player = Object.FindAnyObjectByType<TopDownPlayer>();
            player.enabled = false;
            PatrolEnemy first = GameObject.Find("Patrol / first spear guard").GetComponent<PatrolEnemy>();
            EnemyWeaponLoadout loadout = first.GetComponent<EnemyWeaponLoadout>();
            Assert.That(loadout.Kind, Is.EqualTo(EnemyWeaponKind.Spear));
            Assert.That(first.PatrolRoute.Length, Is.EqualTo(4));
            SpearWeapon spear = loadout.Equipped.GetComponent<SpearWeapon>();
            Assert.That(spear.Body.simulated, Is.False);
            Assert.That(spear.Hitbox.enabled, Is.False);

            first.TakeDamage(999);
            Assert.That(first.gameObject.activeSelf, Is.False);
            Assert.That(spear.transform.parent, Is.Null);
            Assert.That(spear.Body.simulated, Is.True);
            Assert.That(spear.Hitbox.enabled, Is.True);
            player.Body.position = spear.transform.position;
            player.transform.position = spear.transform.position;
            Physics2D.SyncTransforms();
            Assert.That(player.Interact(spear.transform.position), Is.True);
            Assert.That(player.HeldSpear, Is.SameAs(spear));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerHasAWalkableRouteToTheExit()
        {
            TopDownPlayer player = Object.FindAnyObjectByType<TopDownPlayer>();
            FantasyLevelGate gate = Object.FindAnyObjectByType<FantasyLevelGate>();
            const float step = 0.5f;
            const int width = 57;
            const int height = 49;
            var visited = new bool[width, height];
            var queue = new Queue<Vector2Int>();
            Vector2 start = player.transform.position;
            var startCell = new Vector2Int(Mathf.RoundToInt((start.x + 14f) / step),
                Mathf.RoundToInt((start.y + 12f) / step));
            queue.Enqueue(startCell);
            visited[startCell.x, startCell.y] = true;
            bool reached = false;
            Vector2Int[] directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                Vector2 point = new Vector2(-14f + cell.x * step, -12f + cell.y * step);
                if (Vector2.Distance(point, gate.transform.position) < 0.6f)
                {
                    reached = true;
                    break;
                }
                foreach (Vector2Int direction in directions)
                {
                    Vector2Int next = cell + direction;
                    if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height ||
                        visited[next.x, next.y]) continue;
                    visited[next.x, next.y] = true;
                    Vector2 nextPoint = new Vector2(-14f + next.x * step, -12f + next.y * step);
                    if (Physics2D.OverlapCircle(nextPoint, 0.38f, player.Tuning.wallMask)) continue;
                    queue.Enqueue(next);
                }
            }
            Assert.That(reached, Is.True, "The western exit must be reachable through the maze");
            yield return null;
        }
    }
}
