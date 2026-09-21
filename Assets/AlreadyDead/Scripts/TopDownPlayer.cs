using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AlreadyDead
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    [RequireComponent(typeof(UnarmedCombat))]
    public sealed class TopDownPlayer : MonoBehaviour
    {
        [SerializeField] private PrototypeTuning tuning;
        [SerializeField] private Transform facing;
        [SerializeField] private Transform weaponSocket;
        [SerializeField] private AimCamera aimCamera;
        [SerializeField] private UnarmedCombat unarmed;

        private readonly Collider2D[] hoverResults = new Collider2D[16];
        private Rigidbody2D body;
        private Vector2 moveInput;
        private bool fireRequested;
        private bool interactRequested;
        private bool interactReleased;
        private bool cursorReleased;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLock;

        public PistolWeapon HeldWeapon { get; private set; }
        public PistolWeapon HoveredWeapon { get; private set; }
        public SpearWeapon HeldSpear { get; private set; }
        public SpearWeapon HoveredSpear { get; private set; }
        public bool HasWeapon => HeldWeapon != null || HeldSpear != null;
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        public Vector2 AimWorld { get; private set; }
        public bool MovementActive => !cursorReleased && (Application.isFocused || Application.isBatchMode);
        public bool InputActive => MovementActive && PointerInsideGame;
        public PrototypeTuning Tuning => tuning;
        public Rigidbody2D Body => body != null ? body : body = GetComponent<Rigidbody2D>();
        public Transform Facing => facing;
        public AimCamera View => aimCamera;
        public UnarmedCombat Unarmed => unarmed;

        public static bool PointerInsideGame
        {
            get
            {
                if (Mouse.current == null) return false;
                Vector2 p = Mouse.current.position.ReadValue();
                return p.x >= 0 && p.y >= 0 && p.x <= Screen.width && p.y <= Screen.height;
            }
        }

        public void Configure(PrototypeTuning settings, Transform aimRoot, Transform socket, AimCamera view,
            UnarmedCombat unarmedCombat)
        {
            tuning = settings;
            facing = aimRoot;
            weaponSocket = socket;
            aimCamera = view;
            unarmed = unarmedCombat;
        }

        private void Awake() => body = GetComponent<Rigidbody2D>();

        private void OnEnable()
        {
            previousCursorVisible = Cursor.visible;
            previousCursorLock = Cursor.lockState;
            Cursor.lockState = CursorLockMode.None;
        }

        private void OnDisable()
        {
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLock;
            HeldSpear?.CancelCharge();
            SetHovered(null, null);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                cursorReleased = !cursorReleased;

            Cursor.visible = !InputActive;
            moveInput = Vector2.zero;
            fireRequested = false;
            interactRequested = false;
            interactReleased = false;
            if (!MovementActive) return;

            if (keyboard != null)
            {
                moveInput = Vector2.ClampMagnitude(new Vector2(
                    (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)), 1f);
                if (keyboard.rKey.wasPressedThisFrame)
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    return;
                }
            }

            if (!InputActive || Mouse.current == null) return;
            fireRequested = Mouse.current.leftButton.wasPressedThisFrame;
            interactRequested = Mouse.current.rightButton.wasPressedThisFrame;
            interactReleased = Mouse.current.rightButton.wasReleasedThisFrame;
        }

        private void FixedUpdate()
        {
            // World-space input: aiming never rotates the movement axes.
            Body.linearVelocity = moveInput * tuning.moveSpeed;
        }

        private void LateUpdate()
        {
            // Runs after AimCamera.LateUpdate: aim and crosshair use this frame's camera.
            if (!InputActive)
            {
                HeldSpear?.CancelCharge();
                SetHovered(null, null);
                return;
            }

            AimAt(aimCamera.ScreenToWorld(Mouse.current.position.ReadValue()));
            PistolWeapon pistol = FindPickup(AimWorld);
            SpearWeapon spear = FindSpearPickup(AimWorld);
            if (pistol != null && spear != null)
            {
                float pistolDistance = ((Vector2)pistol.transform.position - AimWorld).sqrMagnitude;
                float spearDistance = ((Vector2)spear.transform.position - AimWorld).sqrMagnitude;
                if (pistolDistance <= spearDistance) spear = null;
                else pistol = null;
            }
            SetHovered(pistol, spear);
            if (interactRequested) Interact(AimWorld);
            if (interactReleased) ReleaseSpearThrow();
            if (fireRequested) TryPrimaryAttack();
        }

        public void AimAt(Vector2 worldPosition)
        {
            AimWorld = worldPosition;
            Vector2 delta = worldPosition - (Vector2)transform.position;
            if (delta.sqrMagnitude < 0.0001f) return;
            AimDirection = delta.normalized;
            facing.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        public PistolWeapon FindPickup(Vector2 cursorWorld)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(tuning.weaponMask);
            filter.useTriggers = false;
            int count = Physics2D.OverlapCircle(cursorWorld, tuning.cursorPickupRadius, filter, hoverResults);
            PistolWeapon nearest = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                PistolWeapon weapon = hoverResults[i].GetComponentInParent<PistolWeapon>();
                if (weapon == null || weapon.IsHeld || !CanReach(weapon)) continue;
                float distance = ((Vector2)weapon.transform.position - cursorWorld).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                nearest = weapon;
            }
            return nearest;
        }

        public bool CanReach(PistolWeapon weapon)
        {
            return CanReach(weapon.transform.position);
        }

        public SpearWeapon FindSpearPickup(Vector2 cursorWorld)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(tuning.weaponMask);
            filter.useTriggers = false;
            int count = Physics2D.OverlapCircle(cursorWorld, tuning.cursorPickupRadius, filter, hoverResults);
            SpearWeapon nearest = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                SpearWeapon spear = hoverResults[i].GetComponentInParent<SpearWeapon>();
                if (spear == null || spear.IsHeld || spear.IsFlying || !CanReach(spear.transform.position)) continue;
                float distance = ((Vector2)spear.transform.position - cursorWorld).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                nearest = spear;
            }
            return nearest;
        }

        private bool CanReach(Vector2 destination)
        {
            Vector2 origin = transform.position;
            return (destination - origin).sqrMagnitude <= tuning.pickupDistance * tuning.pickupDistance
                && !Physics2D.Linecast(origin, destination, tuning.wallMask);
        }

        public bool Interact(Vector2 cursorWorld)
        {
            PistolWeapon pickup = FindPickup(cursorWorld);
            SpearWeapon spearPickup = FindSpearPickup(cursorWorld);
            if (pickup != null && spearPickup != null)
            {
                float pistolDistance = ((Vector2)pickup.transform.position - cursorWorld).sqrMagnitude;
                float spearDistance = ((Vector2)spearPickup.transform.position - cursorWorld).sqrMagnitude;
                if (pistolDistance <= spearDistance) spearPickup = null;
                else pickup = null;
            }
            if (pickup != null || spearPickup != null)
            {
                // Picking up another weapon replaces the current one in one click.
                // The previous weapon is left at the player's feet, with no throw impulse.
                if (HeldWeapon != null)
                {
                    HeldWeapon.Drop(this);
                    HeldWeapon = null;
                }
                if (HeldSpear != null)
                {
                    HeldSpear.Drop(this);
                    HeldSpear = null;
                }
                if (pickup != null)
                {
                    HeldWeapon = pickup;
                    pickup.Equip(weaponSocket, this);
                }
                else
                {
                    HeldSpear = spearPickup;
                    spearPickup.Equip(weaponSocket, this);
                }
                unarmed.SetAvailable(false);
                SetHovered(null, null);
                return true;
            }

            if (HeldWeapon != null)
            {
                PistolWeapon thrown = HeldWeapon;
                HeldWeapon = null;
                thrown.Throw(this, AimDirection);
                unarmed.SetAvailable(true);
                SetHovered(null, null);
                return true;
            }

            return HeldSpear != null && HeldSpear.BeginCharge();
        }

        public bool ReleaseSpearThrow()
        {
            if (HeldSpear == null || !HeldSpear.ReleaseThrow(this, AimDirection)) return false;
            HeldSpear = null;
            unarmed.SetAvailable(true);
            return true;
        }

        public bool TryPrimaryAttack()
        {
            if (HeldWeapon != null) return HeldWeapon.TryFire(AimDirection, aimCamera);
            if (HeldSpear != null) return HeldSpear.TryStab(AimDirection);
            return unarmed.TryPunch(AimDirection);
        }

        private void SetHovered(PistolWeapon weapon, SpearWeapon spear)
        {
            if (HoveredWeapon != weapon)
            {
                if (HoveredWeapon != null) HoveredWeapon.SetHighlighted(false);
                HoveredWeapon = weapon;
                if (HoveredWeapon != null) HoveredWeapon.SetHighlighted(true);
            }
            if (HoveredSpear != spear)
            {
                if (HoveredSpear != null) HoveredSpear.SetHighlighted(false);
                HoveredSpear = spear;
                if (HoveredSpear != null) HoveredSpear.SetHighlighted(true);
            }
        }
    }
}
