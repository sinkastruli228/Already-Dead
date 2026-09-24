using UnityEngine;

namespace AlreadyDead
{
    // A hospital guard fires the same Glock or M4 pickup that it drops.
    public sealed class EnemyGlock : MonoBehaviour
    {
        [SerializeField] private PistolWeapon weapon;

        public const float FireRange = 8f;
        public int RemainingAmmo => weapon != null ? weapon.RemainingAmmo : 0;
        public PistolWeapon Weapon => weapon;

        public void Configure(PistolWeapon firearm) => weapon = firearm;

        private void Start()
        {
            if (weapon != null) weapon.HoldForEnemy();
        }

        public bool TryFire(Vector2 direction) =>
            weapon != null && weapon.TryFireFromEnemy(transform.position, direction);

        public void DropOnDeath()
        {
            if (weapon != null) weapon.DropFromEnemy(transform.position);
        }
    }
}
