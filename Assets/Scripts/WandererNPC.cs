using UnityEngine;

/// <summary>Mago: solo hace Wander, ignora al jugador y a los demás NPCs.</summary>
public class WandererNPC : SteeringAgent
{
    protected override Vector2 CalculateSteering()
    {
        return Wander();
    }
}
