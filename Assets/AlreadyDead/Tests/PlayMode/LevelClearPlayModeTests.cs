using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AlreadyDead.Tests
{
    public sealed class LevelClearPlayModeTests
    {
        [Test]
        public void AddedSoundsAreAssigned()
        {
            GameAudioAssets audio = Resources.Load<GameAudioAssets>("GameAudioAssets");
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.door, Is.Not.Null);
            Assert.That(audio.shot, Is.Not.Null);
            Assert.That(audio.reload, Is.Not.Null);
            Assert.That(audio.ricochets, Has.Length.EqualTo(3));
            foreach (AudioClip clip in audio.ricochets) Assert.That(clip, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator MarkedHospitalGuardsHaveEnlargedKnivesAndNoGuns()
        {
            yield return SceneManager.LoadSceneAsync("BuildingParkingScene");
            yield return null;
            foreach (string name in new[]
                { "Hospital guard / 3", "Hospital guard / 6", "Hospital guard / 9 / knife" })
            {
                GameObject guard = GameObject.Find(name);
                Assert.That(guard, Is.Not.Null, name);
                Assert.That(guard.GetComponent<EnemyGlock>(), Is.Null, name);
                Assert.That(guard.GetComponent<EnemyRevolver>(), Is.Null, name);
                Assert.That(guard.GetComponent<EnemyWeaponLoadout>(), Is.Null, name);
                Assert.That(guard.GetComponentInChildren<PistolWeapon>(), Is.Null, name);
                SaloonHandProp hand = guard.GetComponent<SaloonHandProp>();
                Assert.That(hand, Is.Not.Null, name);
                Assert.That(hand.Kind, Is.EqualTo(SaloonHandProp.PropKind.Knife), name);
                Transform knife = null;
                foreach (Transform child in guard.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Held knife / 1.4x") knife = child;
                Assert.That(knife, Is.Not.Null, name);
                Assert.That(knife.localScale.x, Is.EqualTo(0.182f).Within(0.001f), name);
            }
        }

        [UnityTest]
        public IEnumerator CaveGunsUseVisibleUnlitSprites()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            PistolWeapon[] guns = Object.FindObjectsByType<PistolWeapon>(FindObjectsSortMode.None);
            Assert.That(guns.Length, Is.EqualTo(2));
            foreach (PistolWeapon gun in guns)
            {
                int views = 0;
                foreach (SpriteRenderer renderer in gun.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!renderer.name.Contains("side / ground") &&
                        !renderer.name.Contains("top / held")) continue;
                    Assert.That(renderer.sharedMaterial.shader.name,
                        Is.EqualTo("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                    views++;
                }
                Assert.That(views, Is.EqualTo(2), gun.name);
            }
        }

        [UnityTest]
        public IEnumerator PauseExitReturnsToMenuAndRestoresTime()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            PauseMenuController pause = PauseMenuController.EnsureExists();
            pause.OpenPause();
            Assert.That(Time.timeScale, Is.Zero);
            pause.RequestQuit();
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(MainMenuController.MenuSceneName));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(MainMenuController.Instance, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator CaveReturnsToMenuAfterEveryEnemyDies() => CheckLevel("SampleScene");

        [UnityTest]
        public IEnumerator HospitalReturnsToMenuAfterEveryEnemyDies() => CheckLevel("BuildingParkingScene");

        [UnityTest]
        public IEnumerator SaloonReturnsToMenuAfterEveryEnemyDies() => CheckLevel("SaloonScene");

        private static IEnumerator CheckLevel(string sceneName)
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(sceneName);
            yield return null;

            TopDownPlayer player = Object.FindAnyObjectByType<TopDownPlayer>();
            VictorySequence victory = Object.FindAnyObjectByType<VictorySequence>();
            PatrolEnemy[] enemies = Object.FindObjectsByType<PatrolEnemy>(FindObjectsSortMode.None);
            Assert.That(player, Is.Not.Null);
            Assert.That(victory, Is.Not.Null);
            Assert.That(enemies.Length, Is.GreaterThan(0));

            foreach (PatrolEnemy enemy in enemies) enemy.TakeDamage(999);
            Assert.That(VictorySequence.IsFinishing, Is.False);
            yield return new WaitForSeconds(1.8f);
            Assert.That(VictorySequence.IsFinishing, Is.False, "Two-second clear delay must finish first");
            yield return new WaitForSeconds(0.35f);
            Assert.That(VictorySequence.IsFinishing, Is.True);
            Assert.That(player.IsAlive, Is.True);
            yield return new WaitForSeconds(DeathSceneEffect.FadeSeconds + 0.15f);
            Assert.That(victory.ReturnVisible, Is.True);
            Assert.That(player.IsAlive, Is.True);
            yield return new WaitForSeconds(3.15f);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(MainMenuController.MenuSceneName));
            Assert.That(MainMenuController.Instance, Is.Not.Null);
            Assert.That(MainMenuController.Instance.AssetsReady, Is.True);
        }
    }
}
