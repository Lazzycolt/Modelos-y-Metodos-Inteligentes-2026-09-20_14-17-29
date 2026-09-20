using UnityEngine;

/// <summary>
/// Payaso: hace Wander. Si el jugador se acerca (o él se acerca al jugador) huye con Flee.
/// Si se aleja lo suficiente, vuelve a Wander.
/// </summary>
public class CowardNPC : SteeringAgent
{
    [Header("Coward")]
    public float fleeRadius = 5f;   // a esta distancia empieza a huir
    public float safeRadius = 8f;   // más lejos que esto, se calma (mayor que fleeRadius)

    bool fleeing;

    protected override Vector2 CalculateSteering()
    {
        if (!HasPlayer) return Wander();

        float dist = Vector2.Distance(Position, PlayerPosition);

        if (!fleeing && dist < fleeRadius) fleeing = true;
        else if (fleeing && dist > safeRadius) fleeing = false;

        if (fleeing) return Flee(PlayerPosition);
        return Wander();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, fleeRadius);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, safeRadius);
    }
}
