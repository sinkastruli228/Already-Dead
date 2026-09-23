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
        [SerializeField] private PlayerVitality vitality;

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
        public RockWeapon HeldRock { get; private set; }
        public RockWeapon HoveredRock { get; private set; }
        public MagicStaff HeldStaff { get; private set; }
        public MagicStaff HoveredStaff { get; private set; }
        public MusketWeapon HeldMusket { get; private set; }
        public MusketWeapon HoveredMusket { get; private set; }
        public ClubWeapon HeldClub { get; private set; }
        public ClubWeapon HoveredClub { get; private set; }
        public bool HasWeapon => HeldWeapon != null || HeldSpear != null || HeldRock != null ||
            HeldStaff != null || HeldMusket != null || HeldClub != null;
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        public Vector2 AimWorld { get; private set; }
        public bool MovementActive => IsAlive && !cursorReleased && !PauseMenuController.IsPaused &&
            (Application.isFocused || Application.isBatchMode);
        public bool InputActive => MovementActive && PointerInsideGame;
        public bool IsAlive => Vitality == null || Vitality.IsAlive;
        public PlayerVitality Vitality => vitality != null ? vitality : vitality = GetComponent<PlayerVitality>();
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
            SetHovered(null, null, null, null, null, null);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (PauseMenuController.Instance == null && keyboard != null &&
                keyboard.escapeKey.wasPressedThisFrame)
                cursorReleased = !cursorReleased;

            Cursor.visible = !InputActive;
            moveInput = Vector2.zero;
            fireRequested = false;
            interactRequested = false;
            interactReleased = false;
            if (!MovementActive)
            {
                Body.linearVelocity = Vector2.zero;
                return;
            }

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
                if (HeldStaff != null)
                {
                    if (keyboard.digit1Key.wasPressedThisFrame) HeldStaff.SelectElement(MagicElement.Fire);
                    if (keyboard.digit2Key.wasPressedThisFrame) HeldStaff.SelectElement(MagicElement.Frost);
                    if (keyboard.digit3Key.wasPressedThisFrame) HeldStaff.SelectElement(MagicElement.Lightning);
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
                SetHovered(null, null, null, null, null, null);
                return;
            }

            AimAt(aimCamera.ScreenToWorld(Mouse.current.position.ReadValue()));
            PistolWeapon pistol = FindPickup(AimWorld);
            SpearWeapon spear = FindSpearPickup(AimWorld);
            RockWeapon rock = FindRockPickup(AimWorld);
            MagicStaff staff = FindStaffPickup(AimWorld);
            MusketWeapon musket = FindMusketPickup(AimWorld);
            ClubWeapon club = FindClubPickup(AimWorld);
            SelectNearest(AimWorld, ref pistol, ref spear, ref rock, ref staff, ref musket, ref club);
            SetHovered(pistol, spear, rock, staff, musket, club);
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

        public RockWeapon FindRockPickup(Vector2 cursorWorld)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(tuning.weaponMask);
            filter.useTriggers = false;
            int count = Physics2D.OverlapCircle(cursorWorld, tuning.cursorPickupRadius, filter, hoverResults);
            RockWeapon nearest = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RockWeapon rock = hoverResults[i].GetComponentInParent<RockWeapon>();
                if (rock == null || rock.IsHeld || rock.IsFlying || !CanReach(rock.transform.position)) continue;
                float distance = ((Vector2)rock.transform.position - cursorWorld).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                nearest = rock;
            }
            return nearest;
        }

        public MagicStaff FindStaffPickup(Vector2 cursorWorld)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(tuning.weaponMask);
            filter.useTriggers = false;
            int count = Physics2D.OverlapCircle(cursorWorld, tuning.cursorPickupRadius, filter, hoverResults);
            MagicStaff nearest = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                MagicStaff staff = hoverResults[i].GetComponentInParent<MagicStaff>();
                if (staff == null || staff.IsHeld || !CanReach(staff.transform.position)) continue;
                float distance = ((Vector2)staff.transform.position - cursorWorld).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                nearest = staff;
            }
            return nearest;
        }

        public MusketWeapon FindMusketPickup(Vector2 cursorWorld)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(tuning.weaponMask);
            filter.useTriggers = false;
            int count = Physics2D.OverlapCircle(cursorWorld, tuning.cursorPickupRadius, filter, hoverResults);
            MusketWeapon nearest = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                MusketWeapon musket = hoverResults[i].GetComponentInParent<MusketWeapon>();
                if (musket == null || musket.IsHeld || !CanReach(musket.transform.position)) continue;
                float distance = ((Vector2)musket.transform.position - cursorWorld).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                nearest = musket;
            }
            return nearest;
        }

        public ClubWeapon FindClubPickup(Vector2 cursorWorld)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(tuning.weaponMask);
            filter.useTriggers = false;
            int count = Physics2D.OverlapCircle(cursorWorld, tuning.cursorPickupRadius, filter, hoverResults);
            ClubWeapon nearest = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                ClubWeapon club = hoverResults[i].GetComponentInParent<ClubWeapon>();
                if (club == null || club.IsHeld || !CanReach(club.transform.position)) continue;
                float distance = ((Vector2)club.transform.position - cursorWorld).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                nearest = club;
            }
            return nearest;
        }

        private static void SelectNearest(Vector2 cursorWorld, ref PistolWeapon pistol,
            ref SpearWeapon spear, ref RockWeapon rock, ref MagicStaff staff,
            ref MusketWeapon musket, ref ClubWeapon club)
        {
            float best = float.PositiveInfinity;
            Component selected = null;
            if (pistol != null)
            {
                best = ((Vector2)pistol.transform.position - cursorWorld).sqrMagnitude;
                selected = pistol;
            }
            if (spear != null)
            {
                float distance = ((Vector2)spear.transform.position - cursorWorld).sqrMagnitude;
                if (distance < best) { best = distance; selected = spear; }
            }
            if (rock != null)
            {
                float distance = ((Vector2)rock.transform.position - cursorWorld).sqrMagnitude;
                if (distance < best) { best = distance; selected = rock; }
            }
            if (staff != null && ((Vector2)staff.transform.position - cursorWorld).sqrMagnitude < best)
            {
                best = ((Vector2)staff.transform.position - cursorWorld).sqrMagnitude;
                selected = staff;
            }
            if (musket != null && ((Vector2)musket.transform.position - cursorWorld).sqrMagnitude < best)
            {
                best = ((Vector2)musket.transform.position - cursorWorld).sqrMagnitude;
                selected = musket;
            }
            if (club != null && ((Vector2)club.transform.position - cursorWorld).sqrMagnitude < best)
                selected = club;
            if (selected != pistol) pistol = null;
            if (selected != spear) spear = null;
            if (selected != rock) rock = null;
            if (selected != staff) staff = null;
            if (selected != musket) musket = null;
            if (selected != club) club = null;
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
            RockWeapon rockPickup = FindRockPickup(cursorWorld);
            MagicStaff staffPickup = FindStaffPickup(cursorWorld);
            MusketWeapon musketPickup = FindMusketPickup(cursorWorld);
            ClubWeapon clubPickup = FindClubPickup(cursorWorld);
            SelectNearest(cursorWorld, ref pickup, ref spearPickup, ref rockPickup, ref staffPickup,
                ref musketPickup, ref clubPickup);
            if (pickup != null || spearPickup != null || rockPickup != null || staffPickup != null ||
                musketPickup != null || clubPickup != null)
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
                if (HeldRock != null)
                {
                    HeldRock.Drop(this);
                    HeldRock = null;
                }
                if (HeldStaff != null)
                {
                    HeldStaff.Drop(this);
                    HeldStaff = null;
                }
                if (HeldMusket != null)
                {
                    HeldMusket.Drop(this);
                    HeldMusket = null;
                }
                if (HeldClub != null)
                {
                    HeldClub.Drop(this);
                    HeldClub = null;
                }
                if (pickup != null)
                {
                    HeldWeapon = pickup;
                    pickup.Equip(weaponSocket, this);
                }
                else if (spearPickup != null)
                {
                    HeldSpear = spearPickup;
                    spearPickup.Equip(weaponSocket, this);
                }
                else if (rockPickup != null)
                {
                    HeldRock = rockPickup;
                    rockPickup.Equip(weaponSocket, this);
                }
                else if (staffPickup != null)
                {
                    HeldStaff = staffPickup;
                    staffPickup.Equip(weaponSocket, this);
                }
                else if (musketPickup != null)
                {
                    HeldMusket = musketPickup;
                    musketPickup.Equip(weaponSocket, this);
                }
                else
                {
                    HeldClub = clubPickup;
                    clubPickup.Equip(weaponSocket, this);
                }
                unarmed.SetAvailable(false);
                SetHovered(null, null, null, null, null, null);
                return true;
            }

            if (HeldWeapon != null)
            {
                PistolWeapon thrown = HeldWeapon;
                HeldWeapon = null;
                thrown.Throw(this, AimDirection);
                unarmed.SetAvailable(true);
                SetHovered(null, null, null, null, null, null);
                return true;
            }

            if (HeldMusket != null)
            {
                MusketWeapon thrown = HeldMusket;
                HeldMusket = null;
                thrown.Throw(this, AimDirection);
                unarmed.SetAvailable(true);
                SetHovered(null, null, null, null, null, null);
                return true;
            }

            if (HeldClub != null)
            {
                ClubWeapon thrown = HeldClub;
                HeldClub = null;
                thrown.Throw(this, AimDirection);
                unarmed.SetAvailable(true);
                SetHovered(null, null, null, null, null, null);
                return true;
            }

            if (HeldRock != null)
            {
                RockWeapon thrown = HeldRock;
                HeldRock = null;
                thrown.Throw(this, AimDirection);
                unarmed.SetAvailable(true);
                SetHovered(null, null, null, null, null, null);
                return true;
            }

            if (HeldStaff != null)
            {
                MagicStaff dropped = HeldStaff;
                HeldStaff = null;
                dropped.Drop(this);
                unarmed.SetAvailable(true);
                SetHovered(null, null, null, null, null, null);
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
            if (HeldRock != null) return HeldRock.TryStrike(AimDirection);
            if (HeldStaff != null) return HeldStaff.TryCast(AimDirection, aimCamera);
            if (HeldMusket != null) return HeldMusket.TryFire(AimDirection, aimCamera);
            if (HeldClub != null) return HeldClub.TrySwing(AimDirection);
            return unarmed.TryPunch(AimDirection);
        }

        private void SetHovered(PistolWeapon weapon, SpearWeapon spear, RockWeapon rock, MagicStaff staff,
            MusketWeapon musket, ClubWeapon club)
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
            HoveredRock = rock;
            if (HoveredStaff != staff)
            {
                if (HoveredStaff != null) HoveredStaff.SetHighlighted(false);
                HoveredStaff = staff;
                if (HoveredStaff != null) HoveredStaff.SetHighlighted(true);
            }
            if (HoveredMusket != musket)
            {
                if (HoveredMusket != null) HoveredMusket.SetHighlighted(false);
                HoveredMusket = musket;
                if (HoveredMusket != null) HoveredMusket.SetHighlighted(true);
            }
            if (HoveredClub != club)
            {
                if (HoveredClub != null) HoveredClub.SetHighlighted(false);
                HoveredClub = club;
                if (HoveredClub != null) HoveredClub.SetHighlighted(true);
            }
        }
    }
}
