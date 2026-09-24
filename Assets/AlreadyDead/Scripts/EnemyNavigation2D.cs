using UnityEngine;

namespace AlreadyDead
{
    // Shared steering for patrols, fantasy enemies, and saloon guards.
    internal static class EnemyNavigation2D
    {
        public static Vector2 Direction(Vector2 position, Vector2 target, float radius,
            int obstacleMask, ref bool hasDetour, ref Vector2 detourTarget)
        {
            Vector2 toTarget = target - position;
            float distance = toTarget.magnitude;
            if (distance < 0.001f) return Vector2.zero;
            Vector2 direct = toTarget / distance;
            float probeRadius = radius * 0.92f;
            RaycastHit2D hit = Physics2D.CircleCast(position, probeRadius, direct,
                distance, obstacleMask);
            if (!hit)
            {
                hasDetour = false;
                return direct;
            }

            // A closed door is a route, not a wall to walk around. Walk to it and
            // apply a physical push once the enemy reaches the leaf.
            PushDoor2D door = hit.collider.GetComponentInParent<PushDoor2D>();
            if (door != null)
            {
                hasDetour = false;
                if (hit.distance < 1.3f)
                    door.PushFrom(position, direct, 0.16f);
                return direct;
            }

            if (hasDetour)
            {
                Vector2 route = detourTarget - position;
                float length = route.magnitude;
                if (length > 0.24f && !Physics2D.CircleCast(position, probeRadius,
                        route / length, length, obstacleMask)) return route / length;
                hasDetour = false;
            }

            Vector2 bestDirection = Vector2.zero;
            Vector2 chosenTarget = Vector2.zero;
            float bestCost = float.PositiveInfinity;
            Bounds bounds = hit.collider.bounds;
            float margin = radius + 0.22f;
            if (bounds.size.x < 12f && bounds.size.y < 12f)
            {
                Vector2[] corners =
                {
                    new Vector2(bounds.min.x - margin, bounds.min.y - margin),
                    new Vector2(bounds.min.x - margin, bounds.max.y + margin),
                    new Vector2(bounds.max.x + margin, bounds.min.y - margin),
                    new Vector2(bounds.max.x + margin, bounds.max.y + margin)
                };
                foreach (Vector2 corner in corners)
                    TryCandidate(corner);
            }

            Vector2 normal = hit.normal.sqrMagnitude > 0.001f ? hit.normal : -direct;
            Vector2 tangent = new Vector2(-normal.y, normal.x);
            for (int side = -1; side <= 1; side += 2)
                for (int step = 1; step <= 5; step++)
                    TryCandidate(hit.point + normal * margin +
                        tangent * (side * step * (radius + 0.55f)));

            if (bestDirection != Vector2.zero)
            {
                detourTarget = chosenTarget;
                hasDetour = true;
                return bestDirection;
            }

            // Follow the obstacle edge until a direct route or reachable corner opens.
            Vector2 first = tangent;
            Vector2 second = -tangent;
            bool firstOpen = !Physics2D.CircleCast(position, probeRadius, first,
                radius + 0.65f, obstacleMask);
            bool secondOpen = !Physics2D.CircleCast(position, probeRadius, second,
                radius + 0.65f, obstacleMask);
            if (firstOpen || secondOpen)
            {
                Vector2 choice = firstOpen && (!secondOpen ||
                    Vector2.Dot(first, toTarget) >= Vector2.Dot(second, toTarget))
                    ? first : second;
                detourTarget = position + choice * 2f;
                hasDetour = true;
                return choice;
            }
            return direct;

            void TryCandidate(Vector2 candidate)
            {
                Vector2 route = candidate - position;
                float length = route.magnitude;
                if (length < 0.3f ||
                    Physics2D.OverlapCircle(candidate, probeRadius, obstacleMask) != null ||
                    Physics2D.CircleCast(position, probeRadius, route / length,
                        length, obstacleMask)) return;
                float cost = length + Vector2.Distance(candidate, target);
                if (cost >= bestCost) return;
                bestCost = cost;
                bestDirection = route / length;
                chosenTarget = candidate;
            }
        }
    }
}
