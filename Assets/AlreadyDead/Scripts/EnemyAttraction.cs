using System.Collections.Generic;
using UnityEngine;

namespace AlreadyDead
{
    public interface IEnemyAttractionListener
    {
        bool CanInvestigate(Vector2 position, float radius);
        bool CanReactToDeath(Vector2 position, float radius);
        void Investigate(Vector2 position, float radius);
        void InvestigateDeath(Vector2 position, float radius);
    }

    // Gameplay noises are short-lived impulses. Enemies remember the reported point themselves,
    // so rocks and embedded spears do not need permanent trigger objects in the scene.
    public static class EnemyAttraction
    {
        private static readonly HashSet<MonoBehaviour> Notified = new HashSet<MonoBehaviour>();

        public static void Emit(Vector2 position, float radius, LayerMask wallMask,
            bool nearestOnly = false)
        {
            if (radius <= 0f) return;

            Notified.Clear();
            IEnemyAttractionListener nearest = null;
            float nearestDistance = float.PositiveInfinity;
            Collider2D[] listeners = Physics2D.OverlapCircleAll(position, radius);
            foreach (Collider2D listenerCollider in listeners)
            {
                foreach (MonoBehaviour behaviour in listenerCollider.GetComponentsInParent<MonoBehaviour>())
                {
                    if (!(behaviour is IEnemyAttractionListener listener) || !Notified.Add(behaviour))
                        continue;
                    Vector2 listenerPosition = behaviour.transform.position;
                    if (!listener.CanInvestigate(position, radius) ||
                        Physics2D.Linecast(position, listenerPosition, wallMask)) continue;

                    if (!nearestOnly)
                    {
                        listener.Investigate(position, radius);
                        continue;
                    }

                    float distance = Vector2.SqrMagnitude(listenerPosition - position);
                    if (distance >= nearestDistance) continue;
                    nearestDistance = distance;
                    nearest = listener;
                }
            }
            nearest?.Investigate(position, radius);
            Notified.Clear();
        }

        public static void EmitDeath(Vector2 position, float radius, LayerMask wallMask)
        {
            if (radius <= 0f) return;

            Notified.Clear();
            Collider2D[] listeners = Physics2D.OverlapCircleAll(position, radius);
            foreach (Collider2D listenerCollider in listeners)
            {
                foreach (MonoBehaviour behaviour in listenerCollider.GetComponentsInParent<MonoBehaviour>())
                {
                    if (!(behaviour is IEnemyAttractionListener listener) || !Notified.Add(behaviour))
                        continue;
                    if (!listener.CanReactToDeath(position, radius) ||
                        Physics2D.Linecast(position, behaviour.transform.position, wallMask)) continue;
                    listener.InvestigateDeath(position, radius);
                }
            }
            Notified.Clear();
        }
    }
}
