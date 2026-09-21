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
            Assert.That(player, Is.Not.Null);
            Assert.That(pistol, Is.Not.Null);
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
