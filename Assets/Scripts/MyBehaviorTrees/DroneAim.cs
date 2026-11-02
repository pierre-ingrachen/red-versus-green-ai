using UnityEngine;
using UnityEngine.AI;

// Calculs de visée des drones rouges.
public static class DroneAim
{
	// Annexe A : le missile part à la vitesse de l'agent (10 m/s), accélère de 40 m/s² jusqu'à 20 m/s
	const float k_StartSpeed = 10f;
	const float k_Acceleration = 40f;
	const float k_MaxSpeed = 20f;

	public static float MissileFlightTime(float distance)
	{
		float accelerationDistance = (k_MaxSpeed * k_MaxSpeed - k_StartSpeed * k_StartSpeed) / (2 * k_Acceleration);
		if (distance > accelerationDistance) return (k_MaxSpeed - k_StartSpeed) / k_Acceleration + (distance - accelerationDistance) / k_MaxSpeed;

		// le missile accélère encore : d = v0.t + a.t²/2
		return (-k_StartSpeed + Mathf.Sqrt(k_StartSpeed * k_StartSpeed + 2 * k_Acceleration * distance)) / k_Acceleration;
	}

	/// <summary>Position que la cible aura quand un missile tiré maintenant l'atteindra (en supposant qu'elle continue tout droit jusqu'à son point d'arrivée).</summary>
	public static Vector3 PredictPosition(Transform shooter, Transform target)
	{
		Vector3 position = target.position;
		NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
		if (agent == null || !agent.isOnNavMesh || agent.velocity.sqrMagnitude < 0.01f) return position;

		float travel = agent.velocity.magnitude * MissileFlightTime(Vector3.Distance(shooter.position, position));
		if (!agent.pathPending) travel = Mathf.Min(travel, Mathf.Max(0, agent.remainingDistance - agent.stoppingDistance));
		return position + agent.velocity.normalized * travel;
	}
}
