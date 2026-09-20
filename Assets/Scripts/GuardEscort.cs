using UnityEngine;

/// <summary>
/// Escolta (solo grupos de 4). Se pone a los costados del jugador:
///  - jugador se mueve en horizontal -> uno arriba y otro abajo
///  - jugador se mueve en vertical   -> uno a la derecha y otro a la izquierda
/// Si el lugar de la formación está ocupado por un obstáculo, busca la posición libre más cercana.
/// Usar side = 1 en un guardia y side = -1 en el otro.
/// </summary>
public class GuardEscort : SteeringAgent
{
    [Header("Escolta")]
    [Tooltip("+1 = arriba/derecha,  -1 = abajo/izquierda")]
    public int side = 1;
    public float formationDistance = 1.5f;  // separación respecto del jugador
    public float arriveRadius = 1f;         // radio de frenado (Arrive)
    public float searchStep = 0.5f;         // de a cuánto se agranda la búsqueda de un lugar libre
    public float searchMaxRadius = 3f;
    public float minPlayerDistance = 0.9f;  // no pararse encima del jugador

    Rigidbody2D playerRb;
    Vector2 lastPlayerPos;
    bool horizontalMove = true;

    protected override void Awake()
    {
        base.Awake();
        avoidObstacles = true; // la escolta siempre evade obstáculos
        if (player != null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
            lastPlayerPos = player.position;
        }
    }

    protected override Vector2 CalculateSteering()
    {
        if (!HasPlayer) return Vector2.zero;

        UpdateFormationAxis();

        float s = Mathf.Sign(side);
        Vector2 offset = horizontalMove
            ? new Vector2(0f, s * formationDistance)   // jugador horizontal -> arriba/abajo
            : new Vector2(s * formationDistance, 0f);  // jugador vertical   -> derecha/izquierda

        Vector2 goal = FindFreePosition(PlayerPosition + offset);
        return Arrive(goal, arriveRadius, 0f);
    }

    /// <summary>Detecta si el jugador se mueve en horizontal o vertical (si está quieto, mantiene la formación).</summary>
    void UpdateFormationAxis()
    {
        Vector2 pv = playerRb != null
            ? playerRb.linearVelocity
            : (PlayerPosition - lastPlayerPos) / Time.fixedDeltaTime;
        lastPlayerPos = PlayerPosition;

        if (pv.magnitude < 0.1f) return;

        float ax = Mathf.Abs(pv.x), ay = Mathf.Abs(pv.y);
        // el 1.2 evita que la formación parpadee cuando el jugador va en diagonal
        if (horizontalMove && ay > ax * 1.2f) horizontalMove = false;
        else if (!horizontalMove && ax > ay * 1.2f) horizontalMove = true;
    }

    /// <summary>Si 'desired' está libre lo devuelve; si no, busca el punto libre más cercano en anillos crecientes.</summary>
    Vector2 FindFreePosition(Vector2 desired)
    {
        float r = agentRadius;
        if (!IsBlocked(desired, r)) return desired;

        for (float ring = searchStep; ring <= searchMaxRadius; ring += searchStep)
        {
            bool found = false;
            Vector2 best = desired;
            float bestDist = float.MaxValue;

            for (int a = 0; a < 360; a += 30)
            {
                Vector2 p = desired + Rotate(Vector2.right, a) * ring;
                if ((p - PlayerPosition).sqrMagnitude < minPlayerDistance * minPlayerDistance) continue;
                if (IsBlocked(p, r)) continue;

                float d = (p - Position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = p; found = true; }
            }
            if (found) return best;
        }
        return desired;
    }
}
