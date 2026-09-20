using UnityEngine;

/// <summary>
/// Ninja: hace Wander. Si el jugador se acerca (o él se acerca al jugador) lo persigue con Seek
/// y frena con Arrive al llegar. Si el jugador se aleja lo suficiente, vuelve a Wander.
/// </summary>
public class ChaserNPC : SteeringAgent
{
    [Header("Chaser")]
    public float detectionRadius = 5f;  // a esta distancia empieza a perseguir
    public float loseRadius = 8f;       // más lejos que esto, deja de perseguir (mayor que detectionRadius)
    public float arriveRadius = 2f;     // desde acá empieza a frenar (Arrive)
    public float stopDistance = 0.8f;   // distancia a la que se detiene junto al jugador

    bool chasing;

    protected override Vector2 CalculateSteering()
    {
        if (!HasPlayer) return Wander();

        float dist = Vector2.Distance(Position, PlayerPosition);

        if (!chasing && dist < detectionRadius) chasing = true;
        else if (chasing && dist > loseRadius) chasing = false;

        if (!chasing) return Wander();

        if (dist - stopDistance > arriveRadius)
            return Seek(PlayerPosition);
        return Arrive(PlayerPosition, arriveRadius, stopDistance);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, loseRadius);
    }
}
