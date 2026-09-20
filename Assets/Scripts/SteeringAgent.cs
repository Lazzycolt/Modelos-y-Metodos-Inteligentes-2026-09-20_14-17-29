using UnityEngine;

/// <summary>
/// Clase base de todos los NPCs. Contiene los steering behaviors
/// (Seek, Flee, Arrive, Wander) y la detección/evasión de obstáculos.
/// Las clases hijas solo tienen que implementar CalculateSteering().
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public abstract class SteeringAgent : MonoBehaviour
{
    [Header("Movimiento")]
    public float maxSpeed = 3f;
    public float maxForce = 12f;   // aceleración máxima
    public float mass = 1f;

    [Header("Wander")]
    public float wanderDistance = 3f;   // qué tan adelante está el círculo
    public float wanderRadius = 1.5f;   // radio del círculo
    public float wanderJitter = 15f;    // cuánto se mueve el punto por segundo
    [Range(0.1f, 1f)] public float wanderSpeedFactor = 0.5f; // caminar más lento que correr

    [Header("Evasión de obstáculos (grupos de 3 o 4)")]
    public bool avoidObstacles = false;
    public float lookAhead = 1.5f;       // largo del "bigote" central
    public float whiskerAngle = 35f;     // ángulo de los bigotes laterales
    public float avoidWeight = 2f;       // qué tan fuerte gana la evasión sobre el resto

    [Header("Referencias / Visual")]
    public Transform player;             // si queda vacío se busca por tag "Player"
    public bool flipSprite = true;
    public bool spriteFacesRight = true;

    protected Rigidbody2D rb;
    protected float agentRadius = 0.4f;  // se calcula solo a partir del collider
    protected Vector2 heading = Vector2.right;

    Vector2 wanderTarget;
    float avoidSide;
    SpriteRenderer sprite;
    ContactFilter2D filter;

    static readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[16];
    static readonly Collider2D[] overlapBuffer = new Collider2D[16];

    protected Vector2 Position => rb.position;
    protected Vector2 Velocity => rb.linearVelocity;
    protected bool HasPlayer => player != null;
    protected Vector2 PlayerPosition => player.position;

    // ------------------------------------------------------------------
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;        // juego top-down: sin gravedad
        rb.freezeRotation = true;
        if (rb.bodyType == RigidbodyType2D.Kinematic)
            Debug.LogWarning(name + ": el Rigidbody2D es Kinematic, no va a colisionar con paredes. Ponelo en Dynamic.");

        sprite = GetComponentInChildren<SpriteRenderer>();

        Collider2D col = GetComponentInChildren<Collider2D>();
        if (col != null)
            agentRadius = Mathf.Max(0.1f, Mathf.Min(col.bounds.extents.x, col.bounds.extents.y));

        filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = false;

        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
            else Debug.LogWarning(name + ": no encontré al jugador. Ponele el tag 'Player' o arrastralo al campo Player.");
        }

        wanderTarget = Random.insideUnitCircle.normalized * wanderRadius;
        heading = Random.insideUnitCircle.normalized;
        avoidSide = Random.value < 0.5f ? -1f : 1f;
    }

    /// <summary>Cada NPC decide qué fuerza de steering aplicar.</summary>
    protected abstract Vector2 CalculateSteering();

    void FixedUpdate()
    {
        Vector2 vel = rb.linearVelocity;
        if (vel.sqrMagnitude > 0.01f) heading = vel.normalized;

        Vector2 steering = CalculateSteering();
        if (avoidObstacles) steering += ObstacleAvoidance() * avoidWeight;

        steering = Vector2.ClampMagnitude(steering, maxForce);
        vel += steering / mass * Time.fixedDeltaTime;
        rb.linearVelocity = Vector2.ClampMagnitude(vel, maxSpeed);
    }

    void Update()
    {
        if (flipSprite && sprite != null && Mathf.Abs(Velocity.x) > 0.05f)
            sprite.flipX = spriteFacesRight ? Velocity.x < 0f : Velocity.x > 0f;
    }

    // ------------------------------------------------------------------
    //  STEERING BEHAVIORS  (devuelven una fuerza = velocidad deseada - velocidad actual)
    // ------------------------------------------------------------------

    protected Vector2 Seek(Vector2 target, float speed = -1f)
    {
        if (speed < 0f) speed = maxSpeed;
        Vector2 desired = (target - Position).normalized * speed;
        return desired - Velocity;
    }

    protected Vector2 Flee(Vector2 threat, float speed = -1f)
    {
        if (speed < 0f) speed = maxSpeed;
        Vector2 desired = (Position - threat).normalized * speed;
        return desired - Velocity;
    }

    /// <summary>Como Seek pero frena al acercarse. stopDistance = a qué distancia del punto se detiene.</summary>
    protected Vector2 Arrive(Vector2 target, float slowRadius, float stopDistance = 0f, float speed = -1f)
    {
        if (speed < 0f) speed = maxSpeed;
        Vector2 toTarget = target - Position;
        float dist = toTarget.magnitude - stopDistance;
        if (dist <= 0.02f) return -Velocity; // ya llegó: frenar

        float s = dist < slowRadius ? speed * (dist / slowRadius) : speed;
        return toTarget.normalized * s - Velocity;
    }

    /// <summary>Wander: apunta a un punto que se mueve al azar sobre un círculo delante del NPC.</summary>
    protected Vector2 Wander()
    {
        wanderTarget += Random.insideUnitCircle * wanderJitter * Time.fixedDeltaTime;
        wanderTarget = wanderTarget.normalized * wanderRadius;

        Vector2 target = Position + heading * wanderDistance + wanderTarget;
        return Seek(target, maxSpeed * wanderSpeedFactor);
    }

    // ------------------------------------------------------------------
    //  EVASIÓN DE OBSTÁCULOS (3 "bigotes": centro, izquierda y derecha)
    // ------------------------------------------------------------------

    protected Vector2 ObstacleAvoidance()
    {
        Vector2 dir = heading;
        float sideLen = lookAhead * 0.7f;

        bool c = CastObstacle(dir, lookAhead, out RaycastHit2D hc);
        bool l = CastObstacle(Rotate(dir, whiskerAngle), sideLen, out RaycastHit2D hl);
        bool r = CastObstacle(Rotate(dir, -whiskerAngle), sideLen, out RaycastHit2D hr);

        if (!c && !l && !r) return Vector2.zero;

        Vector2 force = Vector2.zero;
        Vector2 leftPerp = new Vector2(-dir.y, dir.x);

        if (c)
        {
            // Obstáculo de frente: doblar hacia el lado que esté libre.
            float proximity = 1f - Mathf.Clamp01(hc.distance / lookAhead);
            if (l && !r) avoidSide = -1f;       // izquierda ocupada -> ir a la derecha
            else if (r && !l) avoidSide = 1f;   // derecha ocupada -> ir a la izquierda
            force += (leftPerp * avoidSide + hc.normal * 0.5f) * (0.5f + proximity);
        }
        if (l) force += hl.normal * (1f - Mathf.Clamp01(hl.distance / sideLen));
        if (r) force += hr.normal * (1f - Mathf.Clamp01(hr.distance / sideLen));

        return Vector2.ClampMagnitude(force * maxForce, maxForce);
    }

    /// <summary>Un obstáculo es cualquier collider sólido que no sea este NPC, otro NPC ni el jugador.</summary>
    bool IsObstacle(Collider2D c)
    {
        if (c == null || c.isTrigger) return false;
        if (c.GetComponentInParent<SteeringAgent>() != null) return false;
        if (player != null && c.transform.IsChildOf(player)) return false;
        return true;
    }

    bool CastObstacle(Vector2 dir, float length, out RaycastHit2D best)
    {
        best = default;
        int n = Physics2D.CircleCast(Position, agentRadius * 0.8f, dir, filter, hitBuffer, length);
        float bestDist = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            if (!IsObstacle(hitBuffer[i].collider)) continue;
            if (hitBuffer[i].distance < bestDist)
            {
                bestDist = hitBuffer[i].distance;
                best = hitBuffer[i];
                found = true;
            }
        }
        return found;
    }

    /// <summary>¿Hay un obstáculo sólido en este punto? (lo usa la escolta)</summary>
    protected bool IsBlocked(Vector2 point, float radius)
    {
        int n = Physics2D.OverlapCircle(point, radius, filter, overlapBuffer);
        for (int i = 0; i < n; i++)
            if (IsObstacle(overlapBuffer[i])) return true;
        return false;
    }

    protected static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }
}
