using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AlreadyDead.Tests
{
    public sealed class PrototypePlayModeTests
    {
        private TopDownPlayer player;
        private PistolWeapon pistol;
        private SpearWeapon spear;
        private RockWeapon rock;
        private MusketWeapon musket;
        private ClubWeapon club;
        private MagicStaff staff;
        private PlayerLimbAnimator limbs;
        private PatrolEnemy enemy;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputTestFixture input;
        private bool projectActionsEnabled;

        [OneTimeSetUp]
        public void IsolateProjectActions()
        {
            projectActionsEnabled = InputSystem.actions != null && InputSystem.actions.enabled;
            InputSystem.actions?.Disable();
        }

        [OneTimeTearDown]
        public void RestoreProjectActions()
        {
            if (projectActionsEnabled) InputSystem.actions?.Enable();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            // UnitySetUp runs before NUnit SetUp. Compose the input fixture here
            // so a later base SetUp cannot reset the devices created in this coroutine.
            input = new InputTestFixture();
            input.Setup();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            player = Object.FindAnyObjectByType<TopDownPlayer>();
            pistol = Object.FindAnyObjectByType<PistolWeapon>();
            spear = Object.FindAnyObjectByType<SpearWeapon>();
            musket = Object.FindAnyObjectByType<MusketWeapon>();
            club = Object.FindAnyObjectByType<ClubWeapon>();
            staff = Object.FindAnyObjectByType<MagicStaff>();
            limbs = Object.FindAnyObjectByType<PlayerLimbAnimator>();
            foreach (PatrolEnemy candidate in Object.FindObjectsByType<PatrolEnemy>())
                if (candidate.name == "Patrol / western flats") enemy = candidate;
            rock = Object.FindObjectsByType<RockWeapon>()[0];
            foreach (RockWeapon candidate in Object.FindObjectsByType<RockWeapon>())
                if (Vector2.Distance(candidate.transform.position, player.transform.position) <
                    Vector2.Distance(rock.transform.position, player.transform.position)) rock = candidate;
            Assert.That(player, Is.Not.Null);
            Assert.That(pistol, Is.Not.Null);
            Assert.That(spear, Is.Not.Null);
            Assert.That(rock, Is.Not.Null);
            Assert.That(musket, Is.Not.Null);
            Assert.That(club, Is.Not.Null);
            Assert.That(staff, Is.Not.Null);
            Assert.That(limbs, Is.Not.Null);
            Assert.That(enemy, Is.Not.Null);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width / 2f, Screen.height / 2f) });
            yield return null;
            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator CleanUpScene()
        {
            if (player != null) player.enabled = false;
            input?.TearDown();
            yield return null;
        }

        [UnityTest]
        public IEnumerator WasdMovesOnWorldAxesAndDiagonalIsNormalized()
        {
            Vector2 start = player.transform.position;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(20, Screen.height * 0.5f) });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(0.2f);
            Assert.That(player.Body.linearVelocity.y, Is.EqualTo(player.Tuning.moveSpeed).Within(0.05f));
            Assert.That(player.Body.linearVelocity.x, Is.EqualTo(0).Within(0.05f));
            Assert.That(player.transform.position.y, Is.GreaterThan(start.y + 0.4f));
            Assert.That(player.AimDirection.x, Is.LessThan(0), "Can walk up while looking left");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
            yield return new WaitForSeconds(0.1f);
            Assert.That(player.Body.linearVelocity.magnitude, Is.EqualTo(player.Tuning.moveSpeed).Within(0.05f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(0.06f);
            Assert.That(player.Body.linearVelocity.magnitude, Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator EnemiesPatrolAndNoticeOnlyAheadWithClearSight()
        {
            Assert.That(Object.FindObjectsByType<PatrolEnemy>().Length, Is.EqualTo(4));
            Vector2 start = enemy.transform.position;
            Assert.That(enemy.CanSeePlayer(), Is.False);
            yield return new WaitForSeconds(0.4f);
            Assert.That(enemy.transform.position.x, Is.GreaterThan(start.x + 0.3f));

            player.Body.position = (Vector2)enemy.transform.position + Vector2.left * 2f;
            player.transform.position = player.Body.position;
            enemy.Facing.rotation = Quaternion.identity;
            Physics2D.SyncTransforms();
            Assert.That(enemy.CanSeePlayer(), Is.False, "Player behind the patrol is not visible");

            player.Body.position = (Vector2)enemy.transform.position + Vector2.right * 2f;
            player.transform.position = player.Body.position;
            Physics2D.SyncTransforms();
            Assert.That(enemy.CanSeePlayer(), Is.True);
            yield return null;
            Assert.That(enemy.Alerted, Is.True);

            enemy.Body.position = new Vector2(3f, 3f);
            enemy.transform.position = enemy.Body.position;
            enemy.Facing.rotation = Quaternion.identity;
            player.Body.position = new Vector2(5f, 3f);
            player.transform.position = player.Body.position;
            Physics2D.SyncTransforms();
            Assert.That(enemy.CanSeePlayer(), Is.False, "Cover blocks enemy vision");
        }

        [UnityTest]
        public IEnumerator SpottedEnemyKeepsChasingAfterLosingSightAndRoutesAroundCover()
        {
            player.enabled = false;
            player.Body.position = (Vector2)enemy.transform.position + Vector2.right * 2f;
            player.transform.position = player.Body.position;
            Physics2D.SyncTransforms();
            Assert.That(enemy.CanSeePlayer(), Is.True);
            yield return null;
            Assert.That(enemy.Alerted, Is.True);

            enemy.Body.position = new Vector2(3f, 3f);
            enemy.transform.position = enemy.Body.position;
            enemy.Body.linearVelocity = Vector2.zero;
            player.Body.position = new Vector2(5f, 3f);
            player.transform.position = player.Body.position;
            Physics2D.SyncTransforms();
            Assert.That(enemy.CanSeePlayer(), Is.False, "Wall hides the player after detection");
            yield return new WaitForSeconds(0.3f);
            Assert.That(Mathf.Abs(enemy.transform.position.y - 3f), Is.GreaterThan(0.1f),
                "The enemy moves around cover instead of stopping at it");
            yield return new WaitForSeconds(2.1f);
            Assert.That(enemy.Alerted, Is.True, "Detection lasts for the rest of the encounter");
            Assert.That(enemy.Body.linearVelocity.magnitude, Is.GreaterThan(0.5f));
            yield return new WaitForSeconds(2f);
            Assert.That(enemy.transform.position.x, Is.GreaterThan(4.4f),
                "The enemy reaches the player's side of the wall");
        }

        [UnityTest]
        public IEnumerator EnemyClosesInAndAttacksPlayerWithCooldown()
        {
            player.enabled = false;
            player.Body.position = (Vector2)enemy.transform.position + Vector2.right * 0.83f;
            player.transform.position = player.Body.position;
            Physics2D.SyncTransforms();
            int initialHealth = player.Vitality.Health;
            yield return new WaitForSeconds(0.12f);
            Assert.That(enemy.Alerted, Is.True);
            Assert.That(enemy.AttacksMade, Is.EqualTo(1));
            Assert.That(player.Vitality.Health, Is.EqualTo(initialHealth - 1));
            yield return new WaitForSeconds(0.2f);
            Assert.That(player.Vitality.Health, Is.EqualTo(initialHealth - 1), "Attack has a cooldown");
        }

        [UnityTest]
        public IEnumerator PlayerCanDefeatPatrolEnemyWithMelee()
        {
            player.enabled = false;
            player.Body.position = (Vector2)enemy.transform.position + Vector2.left * 0.95f;
            player.transform.position = player.Body.position;
            player.AimAt((Vector2)enemy.transform.position);
            Physics2D.SyncTransforms();
            Assert.That(player.TryPrimaryAttack(), Is.True);
            yield return new WaitForSeconds(player.Tuning.punchDuration * 0.6f);
            Assert.That(enemy.Health, Is.EqualTo(player.Tuning.enemyMaxHealth - 1));
            enemy.ReceiveSpear(Vector2.right, 9f);
            Assert.That(enemy.IsAlive, Is.False);
            Assert.That(enemy.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator EnemyHitsAndDeathLeaveDifferentBloodPatterns()
        {
            enemy.enabled = false;
            Assert.That(Object.FindObjectsByType<BloodEffect>().Length, Is.EqualTo(0));
            enemy.TakeDamage(1, Vector2.right);
            BloodEffect first = Object.FindAnyObjectByType<BloodEffect>();
            Assert.That(first.Kind, Is.EqualTo(BloodEffect.BloodKind.Hit));
            enemy.TakeDamage(1, Vector2.up);
            BloodEffect[] afterSecondHit = Object.FindObjectsByType<BloodEffect>();
            Assert.That(afterSecondHit.Length, Is.EqualTo(2));
            Assert.That(afterSecondHit[0].Variant, Is.Not.EqualTo(afterSecondHit[1].Variant));

            enemy.TakeDamage(1, Vector2.right);
            BloodEffect[] effects = Object.FindObjectsByType<BloodEffect>();
            int hits = 0;
            int puddles = 0;
            foreach (BloodEffect effect in effects)
            {
                if (effect.Kind == BloodEffect.BloodKind.Hit) hits++;
                else puddles++;
            }
            Assert.That(hits, Is.EqualTo(3));
            Assert.That(puddles, Is.EqualTo(1));
            Assert.That(enemy.IsAlive, Is.False);
            enemy.TakeDamage(1);
            Assert.That(Object.FindObjectsByType<BloodEffect>().Length, Is.EqualTo(4),
                "Dead enemies do not spawn more blood");

            yield return null;
            Camera camera = player.View.View;
            Vector3 oldPosition = camera.transform.position;
            float oldSize = camera.orthographicSize;
            camera.transform.position = enemy.transform.position + Vector3.back * 10f;
            camera.orthographicSize = 2f;
            var renderTexture = new RenderTexture(640, 480, 24);
            RenderTexture oldActive = RenderTexture.active;
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            var image = new Texture2D(640, 480, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 640, 480), 0, 0);
            image.Apply();
            int redPixels = 0;
            foreach (Color32 pixel in image.GetPixels32())
                if (pixel.r > 90 && pixel.r > pixel.g * 2 && pixel.b < 90) redPixels++;
            Directory.CreateDirectory("Artifacts");
            File.WriteAllBytes("Artifacts/blood-preview.png", image.EncodeToPNG());
            Assert.That(redPixels, Is.GreaterThan(100), "Blood is visible in the rendered scene");
            camera.targetTexture = null;
            RenderTexture.active = oldActive;
            camera.transform.position = oldPosition;
            camera.orthographicSize = oldSize;
            Object.Destroy(image);
            Object.Destroy(renderTexture);

            PatrolEnemy another = Object.FindAnyObjectByType<PatrolEnemy>();
            another.enabled = false;
            another.TakeDamage(another.Health);
            int firstKillVariant = -1;
            int secondKillVariant = -1;
            foreach (BloodEffect effect in Object.FindObjectsByType<BloodEffect>())
            {
                if (effect.Kind != BloodEffect.BloodKind.Kill) continue;
                if (firstKillVariant < 0) firstKillVariant = effect.Variant;
                else secondKillVariant = effect.Variant;
            }
            Assert.That(secondKillVariant, Is.GreaterThanOrEqualTo(0));
            Assert.That(firstKillVariant, Is.Not.EqualTo(secondKillVariant),
                "Death puddles have different pixel-art variants");
        }

        [UnityTest]
        public IEnumerator PlayerCannotWalkThroughOuterWall()
        {
            player.Body.position = new Vector2(-13f, -4f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
            yield return new WaitForSeconds(0.5f);
            Assert.That(player.Body.position.x, Is.GreaterThan(-13.66f));
            Assert.That(player.Body.position.x, Is.LessThan(-13.5f), "Player actually reached the wall");
        }

        [UnityTest]
        public IEnumerator MouseButtonsPickUpFireAndThrow()
        {
            Vector2 pixel = player.View.View.WorldToScreenPoint(pistol.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pixel }.WithButton(MouseButton.Right));
            yield return null;
            yield return null;
            Assert.That(player.HeldWeapon, Is.SameAs(pistol));
            Assert.That(pistol.Body.simulated, Is.False);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pixel });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pixel }.WithButton(MouseButton.Left));
            yield return null;
            yield return null;
            Assert.That(pistol.ShotsFired, Is.EqualTo(1));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pixel });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pixel }.WithButton(MouseButton.Right));
            yield return null;
            yield return null;
            Assert.That(player.HeldWeapon, Is.Null);
            Assert.That(pistol.Body.simulated, Is.True);
            Assert.That(pistol.Body.linearVelocity.magnitude, Is.GreaterThan(1f));
        }

        [Test]
        public void PickupRequiresCursorRangeAndClearLineOfSight()
        {
            Vector2 original = pistol.transform.position;
            Assert.That(player.FindPickup(original), Is.SameAs(pistol));
            Assert.That(player.FindPickup(original + Vector2.up * 2f), Is.Null);
            pistol.Body.position = new Vector2(10, 7);
            pistol.transform.position = new Vector3(10, 7, 0);
            Physics2D.SyncTransforms();
            Assert.That(player.FindPickup(pistol.transform.position), Is.Null, "Too far away");

            player.Body.position = new Vector2(3.2f, 3);
            pistol.Body.position = new Vector2(4.8f, 3);
            player.transform.position = new Vector3(3.2f, 3, 0);
            pistol.transform.position = new Vector3(4.8f, 3, 0);
            Physics2D.SyncTransforms();
            Assert.That(player.CanReach(pistol), Is.False, "Thin wall blocks interaction even within range");
            Assert.That(player.Interact(pistol.transform.position), Is.False);
        }

        [UnityTest]
        public IEnumerator ShotsHaveSpreadCooldownRecoilAndCameraShake()
        {
            player.enabled = false;
            Assert.That(player.Interact(pistol.transform.position), Is.True);
            player.AimAt((Vector2)player.transform.position + Vector2.right * 5);
            float minimum = 100;
            float maximum = -100;
            for (int i = 0; i < 8; i++)
            {
                Assert.That(pistol.TryFire(player.AimDirection, player.View), Is.True);
                float angle = Vector2.SignedAngle(player.AimDirection, pistol.LastShotDirection);
                minimum = Mathf.Min(minimum, angle);
                maximum = Mathf.Max(maximum, angle);
                Assert.That(Mathf.Abs(angle), Is.LessThanOrEqualTo(player.Tuning.spreadHalfAngle + 0.01f));
                Assert.That(pistol.Recoil, Is.GreaterThan(0f));
                Assert.That(player.View.ShakeRemaining, Is.GreaterThan(0f));
                Assert.That(pistol.TryFire(player.AimDirection, player.View), Is.False, "Shot cooldown");
                yield return new WaitForSeconds(player.Tuning.shotInterval + 0.02f);
            }
            Assert.That(maximum - minimum, Is.GreaterThan(0.1f), "Shots should not all share the same trajectory");
            yield return new WaitForSeconds(0.3f);
            Assert.That(pistol.Recoil, Is.LessThan(0.001f));
            Assert.That(player.View.ShakeRemaining, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator FistsAlternateAndAreDisabledWhileHoldingPistol()
        {
            player.enabled = false;
            Assert.That(player.Unarmed.Available, Is.True);
            Assert.That(player.TryPrimaryAttack(), Is.True);
            Assert.That(player.Unarmed.PunchCount, Is.EqualTo(1));
            Assert.That(player.Unarmed.LastPunchUsedLeft, Is.True);
            Assert.That(player.TryPrimaryAttack(), Is.False, "Punch cooldown");
            yield return new WaitForSeconds(player.Tuning.punchInterval + 0.02f);
            Assert.That(player.TryPrimaryAttack(), Is.True);
            Assert.That(player.Unarmed.LastPunchUsedLeft, Is.False);
            yield return new WaitForSeconds(player.Tuning.punchDuration + 0.02f);
            Assert.That(player.Unarmed.IsPunching, Is.False);

            Assert.That(player.Interact(pistol.transform.position), Is.True);
            Assert.That(player.Unarmed.Available, Is.False);
            int punchesBeforeShot = player.Unarmed.PunchCount;
            Assert.That(player.TryPrimaryAttack(), Is.True);
            Assert.That(pistol.ShotsFired, Is.EqualTo(1));
            Assert.That(player.Unarmed.PunchCount, Is.EqualTo(punchesBeforeShot));

            Assert.That(player.Interact(Vector2.zero), Is.True);
            Assert.That(player.Unarmed.Available, Is.True);
            yield return new WaitForSeconds(player.Tuning.punchInterval + 0.02f);
            Assert.That(player.TryPrimaryAttack(), Is.True);
            Assert.That(player.Unarmed.PunchCount, Is.EqualTo(punchesBeforeShot + 1));
        }

        [UnityTest]
        public IEnumerator SpearCanBePickedUpAndStabsInFront()
        {
            player.enabled = false;
            Assert.That(player.FindSpearPickup(spear.transform.position), Is.SameAs(spear));
            Assert.That(player.Interact(spear.transform.position), Is.True);
            Assert.That(player.HeldSpear, Is.SameAs(spear));
            Assert.That(player.Unarmed.Available, Is.False);
            player.Body.position = new Vector2(3.2f, 3f);
            player.transform.position = new Vector3(3.2f, 3f, 0f);
            player.AimAt(new Vector2(8f, 3f));
            Physics2D.SyncTransforms();
            Assert.That(player.TryPrimaryAttack(), Is.True);
            Assert.That(player.TryPrimaryAttack(), Is.False, "Stab cooldown");
            yield return new WaitForSeconds(player.Tuning.spearStabDuration * 0.6f);
            Assert.That(spear.StabsMade, Is.EqualTo(1));
            Assert.That(spear.ImpactsMade, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RocksHaveNoPickupOutlineAndStrikeInFront()
        {
            player.enabled = false;
            Assert.That(Object.FindObjectsByType<RockWeapon>().Length, Is.EqualTo(21));
            Assert.That(rock.transform.Find("Pickup highlight"), Is.Null,
                "Rocks do not use the weapon pickup outline");
            Assert.That(rock.Shadow, Is.Not.Null);
            Assert.That(rock.BuriedMark, Is.Not.Null);
            Assert.That(player.FindRockPickup(rock.transform.position), Is.SameAs(rock));
            Assert.That(player.Interact(rock.transform.position), Is.True);
            Assert.That(player.HeldRock, Is.SameAs(rock));
            Assert.That(player.Unarmed.Available, Is.False);
            player.Body.position = new Vector2(3.2f, 3f);
            player.transform.position = new Vector3(3.2f, 3f, 0f);
            player.AimAt(new Vector2(8f, 3f));
            Physics2D.SyncTransforms();
            Assert.That(player.TryPrimaryAttack(), Is.True);
            Assert.That(player.TryPrimaryAttack(), Is.False, "Rock strike cooldown");
            yield return new WaitForSeconds(player.Tuning.rockStrikeDuration * 0.6f);
            Assert.That(rock.StrikesMade, Is.EqualTo(1));
            Assert.That(rock.ImpactsMade, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RockSwapsAndThrowsImmediatelyOnRightClick()
        {
            player.enabled = false;
            Assert.That(player.Interact(pistol.transform.position), Is.True);
            Assert.That(player.Interact(rock.transform.position), Is.True);
            Assert.That(player.HeldWeapon, Is.Null);
            Assert.That(player.HeldRock, Is.SameAs(rock));
            Assert.That(pistol.Body.linearVelocity, Is.EqualTo(Vector2.zero));
            player.AimAt((Vector2)player.transform.position + Vector2.right * 5f);
            Assert.That(player.Interact(Vector2.zero), Is.True);
            Assert.That(player.HeldRock, Is.Null);
            Assert.That(rock.IsHeld, Is.False);
            Assert.That(rock.IsFlying, Is.True);
            Assert.That(player.FindRockPickup(rock.transform.position), Is.Null,
                "A flying rock cannot be picked back up");
            Assert.That(rock.Body.linearVelocity.magnitude,
                Is.EqualTo(player.Tuning.rockThrowSpeed).Within(0.01f));
            Assert.That(rock.Body.angularVelocity, Is.EqualTo(0f).Within(0.001f));
            Assert.That(rock.transform.eulerAngles.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(player.Unarmed.Available, Is.True);
            yield return new WaitForSeconds(player.Tuning.rockFlightDuration * 0.48f);
            Assert.That(rock.IsFlying, Is.True);
            Assert.That(rock.FlightHeight, Is.GreaterThan(player.Tuning.rockThrowHeight * 0.75f));
            Assert.That(rock.Visual.localPosition.y, Is.GreaterThan(0.2f));
            Assert.That(rock.Shadow.gameObject.activeSelf, Is.True);
            Assert.That(rock.BuriedMark.gameObject.activeSelf, Is.False);
            Assert.That(rock.transform.eulerAngles.z, Is.EqualTo(0f).Within(0.001f));
            yield return new WaitForSeconds(player.Tuning.rockFlightDuration * 0.65f);
            Assert.That(rock.IsFlying, Is.False);
            Assert.That(rock.IsBuried, Is.True);
            Assert.That(rock.Body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(rock.Shadow.gameObject.activeSelf, Is.False);
            Assert.That(rock.BuriedMark.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator CharacterUsesPixelLimbsAndSwitchesArmPoseWithWeapons()
        {
            player.enabled = false;
            Assert.That(player.Unarmed.VisibleArmCount, Is.EqualTo(2));
            Assert.That(player.Unarmed.WeaponArmRaised, Is.False);
            Assert.That(limbs.LeftLeg.GetComponent<SpriteRenderer>().sprite.name, Does.Contain("CaveMan_Leg"));
            Assert.That(limbs.RightLeg.GetComponent<SpriteRenderer>().sprite.name, Does.Contain("CaveMan_Leg"));
            Assert.That(limbs.RightLeg.GetComponent<SpriteRenderer>().flipX, Is.True);
            Assert.That(limbs.RightLeg.GetComponent<SpriteRenderer>().flipY, Is.False,
                "Both legs must keep the brown forward edge facing the same way");
            Transform cavemanBody = null;
            foreach (SpriteRenderer spriteRenderer in player.GetComponentsInChildren<SpriteRenderer>(true))
                if (spriteRenderer.transform.parent == player.Facing && spriteRenderer.sprite != null &&
                    spriteRenderer.sprite.name.Contains("CaveMan_Idle"))
                    cavemanBody = spriteRenderer.transform;
            Assert.That(cavemanBody, Is.Not.Null);
            Assert.That(Mathf.DeltaAngle(cavemanBody.localEulerAngles.z, -90f), Is.EqualTo(0f).Within(0.01f));
            Assert.That(Mathf.DeltaAngle(player.Unarmed.LeftArm.localEulerAngles.z, -90f),
                Is.EqualTo(0f).Within(0.01f));
            Assert.That(player.Unarmed.RightArm.GetComponent<SpriteRenderer>().flipX, Is.True);
            Assert.That(player.Unarmed.RightArm.GetComponent<SpriteRenderer>().flipY, Is.False,
                "Both fists must keep the top of the source sprite facing forward");
            Assert.That(limbs.LeftLeg.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(7));
            Assert.That(player.Unarmed.LeftArm.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(9));
            Assert.That(cavemanBody.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(11),
                "Arms render below the body but above the legs");
            Assert.That(limbs.LeftLeg.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(25));
            Assert.That(player.Unarmed.LeftArm.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(25));
            Assert.That(cavemanBody.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(25),
                "Each body part has a solid two-pixel black outline");

            Transform enemyBody = enemy.Facing.Find("Raider body");
            Transform enemyLeftArm = enemy.Facing.Find("Left arm");
            Transform enemyRightArm = enemy.Facing.Find("Right arm");
            Assert.That(enemyBody, Is.Not.Null);
            Assert.That(enemyLeftArm, Is.Not.Null);
            Assert.That(enemyRightArm, Is.Not.Null);
            Assert.That(enemyRightArm.GetComponent<SpriteRenderer>().flipX, Is.True);
            Assert.That(enemyRightArm.GetComponent<SpriteRenderer>().flipY, Is.False);
            Assert.That(enemyLeftArm.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(9));
            Assert.That(enemyBody.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(11));
            Assert.That(enemyLeftArm.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(25));
            Assert.That(enemyBody.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(25));

            Vector2 startingPosition = player.Body.position;
            player.Body.linearVelocity = Vector2.right * player.Tuning.moveSpeed;
            yield return new WaitForSeconds(0.12f);
            Assert.That(limbs.LeftExtension, Is.GreaterThan(limbs.RightExtension));
            yield return new WaitForSeconds(0.45f);
            Assert.That(limbs.RightExtension, Is.GreaterThan(limbs.LeftExtension));
            player.Body.linearVelocity = Vector2.zero;
            player.Body.position = startingPosition;
            player.transform.position = startingPosition;
            Physics2D.SyncTransforms();

            Assert.That(player.Interact(pistol.transform.position), Is.True);
            Assert.That(player.Unarmed.Available, Is.False);
            Assert.That(player.Unarmed.VisibleArmCount, Is.EqualTo(1));
            Assert.That(player.Unarmed.WeaponArmRaised, Is.True);
            Assert.That(player.Interact(Vector2.zero), Is.True);
            Assert.That(player.Unarmed.VisibleArmCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator NewWeaponSpritesUseTheirIntendedViews()
        {
            player.enabled = false;
            Assert.That(System.Array.Exists(spear.GetComponentsInChildren<SpriteRenderer>(true),
                renderer => renderer.sprite != null && renderer.sprite.name.Contains("Spear")), Is.True);
            Assert.That(musket.GroundViewVisible, Is.True);
            Assert.That(musket.HeldViewVisible, Is.False);

            player.Body.position = (Vector2)musket.transform.position + Vector2.down * 0.5f;
            player.transform.position = player.Body.position;
            Physics2D.SyncTransforms();
            Assert.That(player.Interact(musket.transform.position), Is.True);
            Assert.That(player.HeldMusket, Is.SameAs(musket));
            Assert.That(musket.GroundViewVisible, Is.False);
            Assert.That(musket.HeldViewVisible, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MusketIsLethalShakesCameraAndReloadsFiveTimesSlower()
        {
            player.enabled = false;
            player.Body.position = (Vector2)musket.transform.position + Vector2.down * 0.5f;
            player.transform.position = player.Body.position;
            Physics2D.SyncTransforms();
            Assert.That(player.Interact(musket.transform.position), Is.True);
            Assert.That(musket.ShotInterval,
                Is.EqualTo(player.Tuning.shotInterval * 5f).Within(0.001f));

            player.Body.position = new Vector2(70f, 70f);
            player.transform.position = player.Body.position;
            enemy.enabled = false;
            enemy.Body.position = new Vector2(72f, 70f);
            enemy.transform.position = enemy.Body.position;
            player.AimAt(enemy.transform.position);
            Physics2D.SyncTransforms();
            Assert.That(player.TryPrimaryAttack(), Is.True);
            Assert.That(player.TryPrimaryAttack(), Is.False, "Musket has a long cooldown");
            Assert.That(player.View.ShakeRemaining, Is.GreaterThan(0f));
            yield return new WaitForSeconds(0.12f);
            Assert.That(enemy.IsAlive, Is.False, "A musket shot kills in one hit");
        }

        [UnityTest]
        public IEnumerator ClubSwingsWithArmAndCreatesPixelTrail()
        {
            player.enabled = false;
            player.Body.position = (Vector2)club.transform.position + Vector2.left * 0.5f;
            player.transform.position = player.Body.position;
            Physics2D.SyncTransforms();
            Assert.That(player.Interact(club.transform.position), Is.True);

            player.Body.position = new Vector2(70f, 70f);
            player.transform.position = player.Body.position;
            enemy.enabled = false;
            enemy.Body.position = new Vector2(71f, 70f);
            enemy.transform.position = enemy.Body.position;
            player.AimAt(enemy.transform.position);
            Physics2D.SyncTransforms();
            Quaternion armBefore = player.Unarmed.RightArm.localRotation;
            Assert.That(player.TryPrimaryAttack(), Is.True);
            yield return new WaitForSeconds(player.Tuning.clubSwingDuration * 0.38f);
            Assert.That(Quaternion.Angle(armBefore, player.Unarmed.RightArm.localRotation), Is.GreaterThan(2f));
            Assert.That(club.TrailBursts, Is.EqualTo(1));
            Assert.That(GameObject.Find("Club pixel swing trail"), Is.Not.Null);
            yield return new WaitForSeconds(player.Tuning.clubSwingDuration * 0.25f);
            Assert.That(club.ImpactsMade, Is.EqualTo(1));
            Assert.That(enemy.Health, Is.EqualTo(player.Tuning.enemyMaxHealth - player.Tuning.clubDamage));
        }

        [UnityTest]
        public IEnumerator UniversalStaffSwitchesAbilitiesAndFrostLaunchesFourToSixShards()
        {
            player.enabled = false;
            player.Body.position = (Vector2)staff.transform.position + Vector2.down * 0.5f;
            player.transform.position = player.Body.position;
            Physics2D.SyncTransforms();
            Assert.That(player.Interact(staff.transform.position), Is.True);
            Assert.That(player.HeldStaff, Is.SameAs(staff));

            staff.SelectElement(MagicElement.Fire);
            Assert.That(player.TryPrimaryAttack(), Is.True);
            Assert.That(staff.LastVolleyCount, Is.EqualTo(1));
            yield return new WaitForSeconds(player.Tuning.fireCastInterval + 0.02f);

            staff.SelectElement(MagicElement.Frost);
            Assert.That(player.TryPrimaryAttack(), Is.True);
            Assert.That(staff.LastVolleyCount, Is.InRange(4, 6));
            Assert.That(Object.FindObjectsByType<MagicProjectile>().Length, Is.GreaterThanOrEqualTo(4));
            yield return new WaitForSeconds(player.Tuning.frostCastInterval + 0.02f);

            staff.SelectElement(MagicElement.Lightning);
            Assert.That(player.TryPrimaryAttack(), Is.True);
            Assert.That(staff.SelectedElement, Is.EqualTo(MagicElement.Lightning));
        }

        [UnityTest]
        public IEnumerator WeaponsCanBeSwappedWithoutThrowingTheHeldOne()
        {
            player.enabled = false;
            Assert.That(player.Interact(pistol.transform.position), Is.True);
            Assert.That(player.HeldWeapon, Is.SameAs(pistol));

            Assert.That(player.Interact(spear.transform.position), Is.True);
            Assert.That(player.HeldWeapon, Is.Null);
            Assert.That(player.HeldSpear, Is.SameAs(spear));
            Assert.That(pistol.IsHeld, Is.False);
            Assert.That(pistol.Hitbox.enabled, Is.True);
            Assert.That(pistol.Body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(player.Unarmed.Available, Is.False);

            Physics2D.SyncTransforms();
            Assert.That(player.Interact(pistol.transform.position), Is.True);
            Assert.That(player.HeldWeapon, Is.SameAs(pistol));
            Assert.That(player.HeldSpear, Is.Null);
            Assert.That(spear.IsHeld, Is.False);
            Assert.That(spear.IsCharging, Is.False);
            Assert.That(spear.Hitbox.enabled, Is.True);
            Assert.That(spear.Body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(player.Unarmed.Available, Is.False);
            yield return new WaitForFixedUpdate();
            Assert.That(pistol.IsHeld, Is.True);
        }

        [UnityTest]
        public IEnumerator RightClickOnAnotherWeaponSwapsWithoutStartingSpearThrow()
        {
            Vector2 pistolPixel = player.View.View.WorldToScreenPoint(pistol.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pistolPixel }.WithButton(MouseButton.Right));
            yield return null;
            yield return null;
            Assert.That(player.HeldWeapon, Is.SameAs(pistol));

            InputSystem.QueueStateEvent(mouse, new MouseState { position = pistolPixel });
            yield return null;
            Vector2 spearPixel = player.View.View.WorldToScreenPoint(spear.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = spearPixel }.WithButton(MouseButton.Right));
            yield return null;
            yield return null;
            Assert.That(player.HeldSpear, Is.SameAs(spear));
            Assert.That(player.HeldWeapon, Is.Null);
            Assert.That(pistol.Body.linearVelocity, Is.EqualTo(Vector2.zero));

            InputSystem.QueueStateEvent(mouse, new MouseState { position = spearPixel });
            yield return null;
            Assert.That(spear.IsCharging, Is.False);
            Assert.That(spear.IsFlying, Is.False);
        }

        [UnityTest]
        public IEnumerator WasdStillMovesWhenPointerLeavesGameView()
        {
            Vector2 start = player.transform.position;
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = new Vector2(Screen.width + 100f, Screen.height + 100f)
            });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return new WaitForSeconds(0.15f);
            Assert.That(player.InputActive, Is.False);
            Assert.That(player.MovementActive, Is.True);
            Assert.That(player.Body.linearVelocity.x, Is.EqualTo(player.Tuning.moveSpeed).Within(0.05f));
            Assert.That(player.transform.position.x, Is.GreaterThan(start.x + 0.35f));
        }

        [UnityTest]
        public IEnumerator HoldingSpearThrowLongerIncreasesSpeedAndRange()
        {
            player.enabled = false;
            Vector3 defaultShadowScale = spear.Shadow.localScale;
            Assert.That(player.Interact(spear.transform.position), Is.True);
            Assert.That(spear.Shadow.localScale, Is.EqualTo(defaultShadowScale));
            player.AimAt((Vector2)player.transform.position + Vector2.right * 10f);
            Assert.That(player.Interact(Vector2.zero), Is.True);
            yield return new WaitForSeconds(0.12f);
            Assert.That(player.ReleaseSpearThrow(), Is.True);
            float shortSpeed = spear.LastThrowSpeed;
            float shortRange = spear.LastThrowRange;
            Assert.That(spear.IsFlying, Is.True);
            Assert.That(player.FindSpearPickup(spear.transform.position), Is.Null,
                "A flying spear cannot be picked up");
            yield return new WaitForSeconds(0.12f);
            Assert.That(spear.FlightHeight, Is.GreaterThan(0f));
            Assert.That(spear.Shadow.localScale.x, Is.LessThan(defaultShadowScale.x));
            yield return new WaitForSeconds(0.58f);
            Assert.That(spear.IsFlying, Is.False);
            player.Body.position = spear.transform.position;
            player.transform.position = spear.transform.position;
            Physics2D.SyncTransforms();
            Assert.That(player.Interact(spear.transform.position), Is.True);
            Assert.That(spear.Shadow.localScale, Is.EqualTo(defaultShadowScale));
            Assert.That(player.Interact(Vector2.zero), Is.True);
            yield return new WaitForSeconds(player.Tuning.spearMaxChargeTime + 0.05f);
            Assert.That(spear.Charge01, Is.EqualTo(1f).Within(0.001f));
            Assert.That(player.ReleaseSpearThrow(), Is.True);
            Assert.That(spear.LastThrowSpeed, Is.GreaterThan(shortSpeed));
            Assert.That(spear.LastThrowRange, Is.GreaterThan(shortRange));
            Assert.That(spear.LastThrowSpeed, Is.EqualTo(player.Tuning.spearMaxThrowSpeed).Within(0.01f));
            Assert.That(spear.LastThrowRange, Is.EqualTo(player.Tuning.spearMaxThrowRange).Within(0.01f));
            Assert.That(player.Unarmed.Available, Is.True);
        }

        [UnityTest]
        public IEnumerator ShortChargedSpearThrowDoesNotOneShotEnemy()
        {
            player.enabled = false;
            enemy.enabled = false;
            enemy.Body.linearVelocity = Vector2.zero;
            Assert.That(player.Interact(spear.transform.position), Is.True);
            enemy.Body.position = (Vector2)player.transform.position + Vector2.right * 2.5f;
            enemy.transform.position = enemy.Body.position;
            player.AimAt(enemy.transform.position);
            Physics2D.SyncTransforms();
            Assert.That(player.Interact(Vector2.zero), Is.True);
            yield return new WaitForSeconds(0.12f);
            Assert.That(player.ReleaseSpearThrow(), Is.True);
            Assert.That(spear.LastThrowCharge01, Is.LessThan(1f));
            yield return new WaitForSeconds(0.4f);
            Assert.That(enemy.Health, Is.EqualTo(player.Tuning.enemyMaxHealth - 2));
            Assert.That(enemy.IsAlive, Is.True);
        }

        [UnityTest]
        public IEnumerator FullyChargedSpearThrowOneShotsEnemy()
        {
            player.enabled = false;
            enemy.enabled = false;
            enemy.Body.linearVelocity = Vector2.zero;
            Assert.That(player.Interact(spear.transform.position), Is.True);
            enemy.Body.position = (Vector2)player.transform.position + Vector2.right * 2.5f;
            enemy.transform.position = enemy.Body.position;
            player.AimAt(enemy.transform.position);
            Physics2D.SyncTransforms();
            Assert.That(player.Interact(Vector2.zero), Is.True);
            yield return new WaitForSeconds(player.Tuning.spearMaxChargeTime + 0.05f);
            Assert.That(player.ReleaseSpearThrow(), Is.True);
            Assert.That(spear.LastThrowCharge01, Is.EqualTo(1f).Within(0.001f));
            yield return new WaitForSeconds(0.25f);
            Assert.That(enemy.IsAlive, Is.False);
            Assert.That(enemy.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ThrownSpearStopsAtCover()
        {
            player.enabled = false;
            Assert.That(player.Interact(spear.transform.position), Is.True);
            player.Body.position = new Vector2(3.2f, 3f);
            player.transform.position = new Vector3(3.2f, 3f, 0f);
            player.AimAt(new Vector2(8f, 3f));
            Physics2D.SyncTransforms();
            Assert.That(player.Interact(Vector2.zero), Is.True);
            Assert.That(player.ReleaseSpearThrow(), Is.True);
            yield return new WaitForFixedUpdate();
            Assert.That(spear.IsFlying, Is.False);
            Assert.That(spear.transform.position.x, Is.LessThan(3.675f));
            Assert.That(spear.Hitbox.enabled, Is.True);
            Assert.That(spear.VibrationAmount, Is.GreaterThan(0f));
            Assert.That(spear.Shadow.localScale.x, Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator PunchHasReachAndHitsNearbyWall()
        {
            player.enabled = false;
            player.Body.position = new Vector2(3.2f, 3f);
            player.transform.position = new Vector3(3.2f, 3f, 0);
            player.AimAt(new Vector2(8f, 3f));
            Physics2D.SyncTransforms();
            Assert.That(player.TryPrimaryAttack(), Is.True);
            yield return new WaitForSeconds(player.Tuning.punchDuration * 0.5f);
            Assert.That(player.Unarmed.ImpactCount, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<ShotEffect>().Length, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator CameraFollowsCursorAndNeverExceedsMaximumOffset()
        {
            Assert.That(player.View.LookOffset(Vector2.zero, Vector2.right).magnitude,
                Is.LessThan(player.View.LookOffset(Vector2.zero, Vector2.right * 5f).magnitude));
            Assert.That(player.View.LookOffset(Vector2.zero, Vector2.right * 1000f).magnitude,
                Is.EqualTo(player.Tuning.maxCameraOffset).Within(0.001f));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width - 2, Screen.height * 0.5f) });
            yield return new WaitForSeconds(0.4f);
            Assert.That(player.View.transform.position.x, Is.GreaterThan(player.transform.position.x + 0.3f));
            player.View.Kick();
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                float distance = Vector2.Distance(player.View.transform.position, player.transform.position);
                Assert.That(distance, Is.LessThanOrEqualTo(player.Tuning.maxCameraOffset + 0.001f));
                Vector2 projected = player.View.ScreenToWorld(mouse.position.ReadValue());
                Assert.That(Vector2.Angle(projected - (Vector2)player.transform.position, player.AimDirection),
                    Is.LessThan(0.2f), "Facing stays aligned to visible crosshair after camera movement");
            }
        }

        [UnityTest]
        public IEnumerator ThrownGunBouncesOffWallAndStaysOnPlane()
        {
            player.enabled = false;
            Assert.That(player.Interact(pistol.transform.position), Is.True);
            player.Body.position = new Vector2(-12f, -4f);
            player.transform.position = new Vector3(-12f, -4f, 0);
            player.AimAt(new Vector2(-20f, -4f));
            Physics2D.SyncTransforms();
            Assert.That(player.Interact(Vector2.zero), Is.True);
            Assert.That(pistol.Body.linearVelocity.x, Is.LessThan(-1));
            bool bounced = false;
            for (int i = 0; i < 35; i++)
            {
                yield return new WaitForFixedUpdate();
                if (pistol.Body.linearVelocity.x > 0.2f) bounced = true;
                Assert.That(pistol.Body.position.x, Is.GreaterThan(-14f));
                Assert.That(pistol.transform.position.z, Is.EqualTo(0).Within(0.0001f));
            }
            Assert.That(bounced, Is.True);
            yield return new WaitForSeconds(3f);
            Assert.That(pistol.Body.linearVelocity.magnitude, Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator GunCanBePickedUpAfterThrow()
        {
            player.enabled = false;
            Assert.That(player.Interact(pistol.transform.position), Is.True);
            player.AimAt((Vector2)player.transform.position + Vector2.right * 4f);
            Assert.That(player.Interact(Vector2.zero), Is.True);
            yield return new WaitForFixedUpdate();
            Assert.That(player.Interact(pistol.transform.position), Is.True);
            Assert.That(pistol.IsHeld, Is.True);
            Assert.That(pistol.Body.linearVelocity, Is.EqualTo(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator FastBulletsHitWallsAndBlockedMuzzleCannotShootThroughCover()
        {
            player.enabled = false;
            Assert.That(player.Interact(pistol.transform.position), Is.True);
            player.AimAt((Vector2)player.transform.position + Vector2.right * 10f);
            Assert.That(pistol.TryFire(player.AimDirection, player.View), Is.True);
            Projectile bullet = Object.FindAnyObjectByType<Projectile>();
            Assert.That(bullet, Is.Not.Null);
            bullet.transform.position = new Vector3(3f, 3f, 0);
            bullet.Step(0.1f); // 4.2 units in one step, through a 0.65-unit wall.
            Assert.That(bullet.gameObject.activeSelf, Is.False);
            yield return new WaitForSeconds(0.2f);

            player.Body.position = new Vector2(3.26f, 3f);
            player.transform.position = new Vector3(3.26f, 3f, 0);
            player.AimAt(new Vector2(8f, 3f));
            Physics2D.SyncTransforms();
            Assert.That(pistol.TryFire(player.AimDirection, player.View), Is.True);
            Assert.That(Object.FindObjectsByType<Projectile>().Length, Is.EqualTo(0));
            Assert.That(Object.FindObjectsByType<ShotEffect>().Length, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator ThrowAgainstWallDoesNotSpawnGunThroughIt()
        {
            player.enabled = false;
            Assert.That(player.Interact(pistol.transform.position), Is.True);
            player.Body.position = new Vector2(3.26f, 3f);
            player.transform.position = new Vector3(3.26f, 3f, 0);
            player.AimAt(new Vector2(8f, 3f));
            Physics2D.SyncTransforms();
            player.Interact(Vector2.zero);
            for (int i = 0; i < 15; i++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(pistol.Body.position.x, Is.LessThan(3.675f));
            }
        }

        [UnityTest]
        public IEnumerator SceneRendersWithPlayerAndPistol()
        {
            yield return new WaitForSeconds(0.1f);
            Camera camera = player.View.View;
            Vector3 oldPosition = camera.transform.position;
            float oldSize = camera.orthographicSize;
            // A wide overview makes the entire collision playground reviewable.
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographicSize = 10.5f;
            var renderTexture = new RenderTexture(1600, 1000, 24);
            RenderTexture oldActive = RenderTexture.active;
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Artifacts");
            File.WriteAllBytes("Artifacts/prototype-overview.png", image.EncodeToPNG());
            Assert.That(image.GetPixel(800, 500).maxColorComponent, Is.GreaterThan(0.03f));
            camera.targetTexture = null;
            RenderTexture.active = oldActive;
            camera.transform.position = oldPosition;
            camera.orthographicSize = oldSize;
            Object.Destroy(image);
            Object.Destroy(renderTexture);
            ScreenCapture.CaptureScreenshot("Artifacts/prototype-gameplay.png");
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTest]
        public IEnumerator EscapeReleasesAndRestoresControl()
        {
            Assert.That(player.InputActive, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape, Key.W));
            yield return new WaitForSeconds(0.05f);
            Assert.That(player.InputActive, Is.False);
            Assert.That(player.Body.linearVelocity.magnitude, Is.LessThan(0.01f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            Assert.That(player.InputActive, Is.True);
        }

        [UnityTest]
        public IEnumerator ResetKeyReloadsPlayableScene()
        {
            Assert.That(player.Interact(pistol.transform.position), Is.True);
            TopDownPlayer original = player;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(0.1f);
            player = Object.FindAnyObjectByType<TopDownPlayer>();
            Assert.That(original == null, Is.True);
            Assert.That(player, Is.Not.Null);
            Assert.That(player.HeldWeapon, Is.Null);
            Assert.That(Vector2.Distance(player.transform.position, new Vector2(-8, -4)), Is.LessThan(0.01f));
        }
    }
}
