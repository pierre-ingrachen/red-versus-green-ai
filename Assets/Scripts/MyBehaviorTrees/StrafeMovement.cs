using UnityEngine;
using UnityEngine.AI;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

// Les drones verts tirent toujours vers la position ACTUELLE de leur cible, sans anticiper, et le missile met 0,4 s à plus d'1 s pour arriver.
// Un drone qui ne s'arrête jamais et se déplace perpendiculairement à la ligne de tir de l'ennemi le plus proche est donc très difficile à toucher.
// La vitesse (10 m/s) et la vitesse angulaire (180°/s) sont celles de l'Annexe A ; l'accélération (8 m/s²) est celle du prefab.
public abstract class StrafeMovement : Action
{
	public SharedTransform target;

	// Annexe A, action My Seek : le prefab laisse la vitesse de l'agent à 0,1, c'est MySeek qui la règle au démarrage. Comme on saute MySeek quand la cible est
	// à portée, on applique ici les mêmes valeurs (elles ne sont pas modifiées).
	const float k_Speed = 10f;
	const float k_AngularSpeed = 180f;

	const float k_ThreatRange = 25f;    // ennemis pris en compte pour l'esquive
	const float k_MinTargetDistance = 6f;
	const float k_MaxTargetDistance = 18f;
	const float k_SpacingDistance = 14f; // 2 x le rayon de dégâts des grenades (5 m) : une grenade ne doit toucher qu'un seul drone
	const bool k_DodgeGrenades = true;      // test : l'esquive des grenades détourne les drones de leur cible
	const float k_GrenadeDangerRadius = 8f; // rayon de dégâts des grenades (Annexe A : 5 m) + marge
	const float k_DodgeSpeed = 8f;          // vitesse moyenne retenue pour une fuite (accélération limitée à 8 m/s²)
	const float k_StepLength = 14f;     // destination lointaine : l'agent ne freine jamais et garde sa pleine vitesse

	protected NavMeshAgent m_Agent;
	ArmyElement m_ArmyElement;
	readonly System.Collections.Generic.List<ArmyManagerRed.IncomingImpact> m_Impacts = new System.Collections.Generic.List<ArmyManagerRed.IncomingImpact>();
	float m_Direction;
	float m_NextFlip;
	float m_NextRepath;

	public override void OnAwake()
	{
		m_Agent = GetComponent<NavMeshAgent>();
		m_ArmyElement = GetComponent<ArmyElement>();
	}

	public override void OnStart()
	{
		m_Agent.speed = k_Speed;
		m_Agent.angularSpeed = k_AngularSpeed;
		m_Agent.isStopped = false;
		m_Agent.updateRotation = false; // le drone avance de côté en restant tourné vers sa cible
		m_Agent.stoppingDistance = 0;   // MySeek l'a réglée à 6 m : en dessous, l'agent se croirait arrivé et ne bougerait presque pas (rétablie par MySeek à la boucle suivante)
		m_Direction = Random.value < .5f ? -1 : 1;
		m_NextFlip = Time.time + Random.Range(2f, 3.5f);
		m_NextRepath = 0;
	}

	protected void Strafe()
	{
		if (Dodge()) return;

		if (Time.time >= m_NextFlip)
		{
			m_Direction = -m_Direction;
			m_NextFlip = Time.time + Random.Range(2f, 3.5f);
		}

		if (Time.time < m_NextRepath) return;
		m_NextRepath = Time.time + 0.15f;

		Vector3 position = transform.position;
		Vector3 toTarget = target.Value ? Vector3.ProjectOnPlane(target.Value.position - position, Vector3.up) : Vector3.zero;

		// on tourne autour de l'ennemi drone le plus proche (le plus dangereux), ou de notre cible s'il n'y en a pas
		Vector3 reference = position + toTarget;
		ArmyManagerRed armyManager = m_ArmyElement != null ? m_ArmyElement.ArmyManager as ArmyManagerRed : null;
		Vector3 threat;
		if (armyManager != null && armyManager.TryGetNearestEnemyDrone(position, k_ThreatRange, out threat)) reference = threat;

		Vector3 toReference = Vector3.ProjectOnPlane(reference - position, Vector3.up);
		if (toReference.sqrMagnitude < 0.01f) toReference = transform.forward;
		Vector3 direction = Vector3.Cross(Vector3.up, toReference.normalized) * m_Direction;

		// on reste à bonne distance de notre cible
		float targetDistance = toTarget.magnitude;
		if (targetDistance > k_MaxTargetDistance) direction += toTarget.normalized * 0.7f;
		else if (targetDistance < k_MinTargetDistance && targetDistance > 0.01f) direction -= toTarget.normalized * 0.7f;

		// on s'écarte des autres drones rouges : serrés les uns contre les autres, ils prennent tous la même grenade (dégâts de zone)
		Vector3 friend;
		if (armyManager != null && armyManager.TryGetNearestFriendlyDrone(position, k_SpacingDistance, out friend))
		{
			Vector3 away = Vector3.ProjectOnPlane(position - friend, Vector3.up);
			if (away.sqrMagnitude > 0.01f) direction += away.normalized * 2f;
		}

		MoveTowards(direction.normalized);
	}

	void MoveTowards(Vector3 direction)
	{
		NavMeshHit hit;
		Vector3 position = transform.position;
		if (NavMesh.SamplePosition(position + direction * k_StepLength, out hit, 4f, NavMesh.AllAreas) ||
			NavMesh.SamplePosition(position + direction * k_StepLength * 0.5f, out hit, 4f, NavMesh.AllAreas))
		{
			m_Agent.SetDestination(hit.position);
		}
		else m_Direction = -m_Direction; // obstacle : on part dans l'autre sens
	}

	// des grenades ennemies sont en l'air (le chef d'état-major voit leur trajectoire) : si l'une d'elles doit tomber sur nous, on prend la direction
	// qui nous éloigne le mieux de TOUS les impacts prévus (il y en a une dizaine en même temps face à 9 tourelles)
	bool Dodge()
	{
		if (!k_DodgeGrenades) return false;

		ArmyManagerRed armyManager = m_ArmyElement != null ? m_ArmyElement.ArmyManager as ArmyManagerRed : null;
		if (armyManager == null) return false;

		armyManager.GetIncomingImpacts(m_Impacts);
		if (m_Impacts.Count == 0) return false;

		Vector3 position = transform.position;
		if (Danger(position, m_Agent.velocity) <= 0) return false; // en continuant ainsi, aucune grenade ne nous touche

		if (Time.time >= m_NextRepath)
		{
			m_NextRepath = Time.time + 0.1f;

			float bestDanger = float.MaxValue;
			Vector3 bestDirection = transform.forward;
			for (int i = 0; i < 16; i++)
			{
				Vector3 direction = Quaternion.AngleAxis(i * 22.5f, Vector3.up) * Vector3.forward;
				float danger = Danger(position, direction * k_DodgeSpeed);
				if (danger < bestDanger) { bestDanger = danger; bestDirection = direction; }
			}
			MoveTowards(bestDirection);
		}
		return true;
	}

	// somme, pour chaque grenade en vol, de la pénétration dans sa zone de dégâts à l'instant de l'impact, si on se déplace à la vitesse velocity
	float Danger(Vector3 position, Vector3 velocity)
	{
		float danger = 0;
		foreach (ArmyManagerRed.IncomingImpact impact in m_Impacts)
		{
			Vector3 predicted = position + velocity * (impact.Time - Time.time);
			float distance = Vector2.Distance(new Vector2(predicted.x, predicted.z), new Vector2(impact.Position.x, impact.Position.z));
			danger += Mathf.Max(0, k_GrenadeDangerRadius - distance);
		}
		return danger;
	}
}
