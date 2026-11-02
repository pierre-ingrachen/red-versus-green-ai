using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.AI;

public class ArmyManagerRed : ArmyManager
{
	// ---------------------------------------------------------------------------------
	// FOCUS FIRE : toutes les unités rouges se concentrent sur la même cible, au lieu de
	// choisir un ennemi au hasard comme le fait l'armée verte.
	// ---------------------------------------------------------------------------------

	const float k_DistanceWeight = 1f;          // "PV équivalents" ajoutés au score par mètre entre la cible et le barycentre de l'armée
	const float k_FocusStickiness = 15f;        // bonus du score de la cible déjà focalisée (évite de changer de cible à tout bout de champ)
	const float k_RocketDamage = 10f;           // Annexe A : Rocket / Damage Points
	const float k_RocketTravelDuration = 4f;    // Annexe A : Turret / Rocket Travel Duration

	class ShotReservation
	{
		public float Damage;
		public float ExpireTime;
	}

	// cible actuellement focalisée, par type d'unité (Drone, Turret)
	readonly Dictionary<System.Type, ArmyElement> m_Focus = new Dictionary<System.Type, ArmyElement>();
	const float k_DamagePerSalvo = 10f;         // une salve de drone = 2 missiles de 5 PV (Annexe A) : sûrs de toucher une tourelle fixe
	const float k_ExpectedDamagePerDrone = 7f; // une salve de drone = 10 PV (2 missiles de 5), dont on compte environ 70 % de touches

	// pour chaque ennemi, les unités rouges qui lui sont assignées
	readonly Dictionary<ArmyElement, List<ArmyElement>> m_Assignments = new Dictionary<ArmyElement, List<ArmyElement>>();

	const float k_DangerRadius = 7f;   // rayon de dégâts de la grenade (Annexe A : 5 m) + marge
	const float k_FleeMaxDelay = 2f;    // on ne s'écarte que dans les 2 dernières secondes avant l'impact
	const float k_FleeMinDelay = 0.2f;  // trop tard pour fuir en dessous

	struct IncomingImpact
	{
		public Vector3 Position;
		public float Time; // date d'impact
	}

	// points d'impact des grenades ennemies en vol, recalculés au plus tous les 0,1 s
	readonly List<IncomingImpact> m_Impacts = new List<IncomingImpact>();
	float m_ImpactsRefreshTime = -1;
	string m_RedTag;

	// point de visée (invisible) de chaque tourelle : sert à viser l'endroit où un drone sera, et non où il est
	readonly Dictionary<Turret, Transform> m_AimPoints = new Dictionary<Turret, Transform>();
	// dégâts de grenades "réservés" sur une cible : tirs déjà décidés mais pas encore tombés
	readonly Dictionary<ArmyElement, List<ShotReservation>> m_Reservations = new Dictionary<ArmyElement, List<ShotReservation>>();

	Vector3 ArmyCenter()
	{
		var positions = m_ArmyElements.OfType<Component>().Where(c => c != null).Select(c => c.transform.position).ToList();
		if (positions.Count == 0) return Vector3.zero;
		return positions.Aggregate(Vector3.zero, (sum, pos) => sum + pos) / positions.Count;
	}

	float ReservedDamage(ArmyElement enemy)
	{
		List<ShotReservation> reservations;
		if (!m_Reservations.TryGetValue(enemy, out reservations)) return 0;
		reservations.RemoveAll(item => item.ExpireTime < Time.time);
		return reservations.Sum(item => item.Damage);
	}

	/// <summary>
	/// Désigne l'ennemi de type T sur lequel concentrer le feu, parmi ceux situés dans l'anneau [minRadius, maxRadius] autour de fromPos.
	/// Les PV restants (moins les grenades déjà en route) priment, puis la proximité avec le barycentre de l'armée.
	/// Tous les appelants obtiennent donc la même cible tant qu'elle est à leur portée.
	/// </summary>
	public GameObject GetFocusEnemy<T>(Vector3 fromPos, float minRadius, float maxRadius) where T : ArmyElement
	{
		Vector3 center = ArmyCenter();
		ArmyElement currentFocus;
		m_Focus.TryGetValue(typeof(T), out currentFocus);

		ArmyElement best = null;
		float bestScore = float.MaxValue;
		ArmyElement doomed = null; // cible déjà condamnée par les grenades en vol : dernier recours s'il n'y en a pas d'autre
		float doomedHealth = float.MinValue;

		foreach (T enemy in GetAllEnemiesOfType<T>(false))
		{
			float distance = Vector3.Distance(fromPos, enemy.transform.position);
			if (distance <= minRadius || distance >= maxRadius) continue;

			float effectiveHealth = enemy.Health - ReservedDamage(enemy);
			if (effectiveHealth <= 0)
			{
				if (effectiveHealth > doomedHealth) { doomed = enemy; doomedHealth = effectiveHealth; }
				continue;
			}

			float score = effectiveHealth + k_DistanceWeight * Vector3.Distance(center, enemy.transform.position);
			if (enemy == currentFocus) score -= k_FocusStickiness;

			if (score < bestScore) { best = enemy; bestScore = score; }
		}

		ArmyElement target = best != null ? best : doomed;
		if (target != null) m_Focus[typeof(T)] = target;
		return target != null ? target.gameObject : null;
	}

	int AssignedCount(ArmyElement enemy)
	{
		List<ArmyElement> assigned;
		if (!m_Assignments.TryGetValue(enemy, out assigned)) return 0;
		assigned.RemoveAll(item => item == null);
		return assigned.Count;
	}

	/// <summary>
	/// Répartit les unités rouges par groupes sur les ennemis de type T : les ennemis les plus avancés vers le centre de notre armée en premier,
	/// chacun recevant juste assez d'unités pour être détruit (PV / dégâts attendus par unité), les suivantes passant à l'ennemi d'après.
	/// current (la cible précédente de l'appelant) est conservée tant qu'elle est vivante : on tire dessus jusqu'à sa mort.
	/// </summary>
	public GameObject AssignTarget<T>(ArmyElement caller, float minRadius, float maxRadius, ArmyElement current) where T : ArmyElement
	{
		if (current != null && current is T && current.Health > 0 && current.ArmyManager != this) return current.gameObject;

		foreach (ArmyElement deadEnemy in m_Assignments.Keys.Where(key => key == null).ToList()) m_Assignments.Remove(deadEnemy);
		foreach (List<ArmyElement> assigned in m_Assignments.Values) assigned.Remove(caller);

		Vector3 center = ArmyCenter();
		Vector3 callerPosition = caller.transform.position;
		List<T> candidates = GetAllEnemiesOfType<T>(false)
			.Where(enemy =>
			{
				float distance = Vector3.Distance(callerPosition, enemy.transform.position);
				return enemy.Health > 0 && distance > minRadius && distance < maxRadius;
			})
			// drones ennemis : on achève d'abord les plus blessés (un ennemi mort ne tire plus), les plus proches départageant ;
			// tourelles ennemies (fixes, intactes) : les plus avancées vers le centre de notre armée d'abord
			.OrderBy(enemy => enemy is Drone ? enemy.Health + Vector3.Distance(callerPosition, enemy.transform.position) : Vector3.Distance(center, enemy.transform.position))
			.ToList();

		ArmyElement chosen = null;
		foreach (T enemy in candidates)
		{
			if (AssignedCount(enemy) < Mathf.CeilToInt(enemy.Health / (enemy is Turret ? k_DamagePerSalvo : k_ExpectedDamagePerDrone))) { chosen = enemy; break; }
		}
		if (chosen == null && candidates.Count > 0) chosen = candidates[0]; // tous les ennemis ont leur compte : on renforce le plus avancé

		if (chosen == null) return null;

		List<ArmyElement> group;
		if (!m_Assignments.TryGetValue(chosen, out group))
		{
			group = new List<ArmyElement>();
			m_Assignments[chosen] = group;
		}
		group.Add(caller);
		return chosen.gameObject;
	}

	/// <summary>
	/// Phase 2 de la bataille : toutes nos tourelles sont détruites alors qu'il reste des drones ennemis. Les tourelles ennemies ne ciblent plus nos tourelles
	/// mais nos drones (qui esquivent leurs grenades) : les drones rouges passent au combat contre les drones ennemis.
	/// </summary>
	public bool ShouldFightEnemyDrones()
	{
		if (m_ArmyElements.Any(element => element is Turret)) return false;
		return GetAllEnemiesOfType<Drone>(false).Any(enemy => enemy.Health > 0);
	}

	// le chef d'état-major voit la trajectoire des grenades : on intègre la balistique (vitesse actuelle + gravité) jusqu'au sol
	void RefreshImpacts()
	{
		if (Time.time - m_ImpactsRefreshTime < 0.1f) return;
		m_ImpactsRefreshTime = Time.time;
		m_Impacts.Clear();

		if (m_RedTag == null)
		{
			Component first = m_ArmyElements.OfType<Component>().FirstOrDefault(c => c != null);
			if (first == null) return;
			m_RedTag = first.tag;
		}

		const float step = 0.05f;
		foreach (Rocket rocket in FindObjectsByType<Rocket>(FindObjectsSortMode.None))
		{
			if (rocket.CompareTag(m_RedTag)) continue;
			Rigidbody body = rocket.GetComponent<Rigidbody>();
			if (body == null) continue;

			Vector3 position = body.position;
			Vector3 velocity = body.linearVelocity;
			for (float t = step; t < 5f; t += step)
			{
				velocity += Physics.gravity * step;
				position += velocity * step;

				Vector3 ground = Vector3.zero, normal = Vector3.zero;
				if (TerrainManager.Instance.GetVerticallyAlignedPositionOnTerrain(position, ref ground, ref normal) && position.y <= ground.y + 1f)
				{
					m_Impacts.Add(new IncomingImpact { Position = position, Time = Time.time + t });
					break;
				}
			}
		}
	}

	/// <summary>
	/// Vrai si une grenade ennemie va tomber à moins de k_DangerRadius de position dans les prochaines secondes (et qu'il est encore temps de s'écarter).
	/// </summary>
	public bool TryGetIncomingImpact(Vector3 position, out Vector3 impact)
	{
		RefreshImpacts();

		impact = Vector3.zero;
		float nearest = float.MaxValue;
		foreach (IncomingImpact incoming in m_Impacts)
		{
			float remaining = incoming.Time - Time.time;
			if (remaining < k_FleeMinDelay || remaining > k_FleeMaxDelay) continue;

			float distance = Vector3.Distance(position, incoming.Position);
			if (distance < k_DangerRadius && distance < nearest) { nearest = distance; impact = incoming.Position; }
		}
		return nearest < float.MaxValue;
	}

	/// <summary>Point où se placer pour sortir de la zone de dégâts d'une grenade qui tombe en impact.</summary>
	public Vector3 EscapePoint(Vector3 position, Vector3 impact)
	{
		Vector3 away = Vector3.ProjectOnPlane(position - impact, Vector3.up);
		if (away.sqrMagnitude < 0.01f) away = Vector3.right;
		return impact + away.normalized * (k_DangerRadius + 3f);
	}

	/// <summary>
	/// Ennemi de type T le plus proche de fromPos, dans l'anneau [minRadius, maxRadius].
	/// Si current (la cible précédente de l'appelant) est toujours vivante, on la garde : on tire dessus jusqu'à sa mort.
	/// </summary>
	public GameObject GetNearestEnemy<T>(Vector3 fromPos, float minRadius, float maxRadius, ArmyElement current) where T : ArmyElement
	{
		if (current != null && current is T && current.Health > 0 && current.ArmyManager != this) return current.gameObject;

		ArmyElement nearest = null;
		float nearestDistance = float.MaxValue;
		foreach (T enemy in GetAllEnemiesOfType<T>(false))
		{
			if (enemy.Health <= 0) continue;

			float distance = Vector3.Distance(fromPos, enemy.transform.position);
			if (distance <= minRadius || distance >= maxRadius) continue;

			if (distance < nearestDistance) { nearest = enemy; nearestDistance = distance; }
		}
		return nearest != null ? nearest.gameObject : null;
	}

	// position probable d'un ennemi dans delay secondes, d'après sa vitesse (et en s'arrêtant à son point d'arrivée)
	Vector3 PredictPosition(ArmyElement enemy, float delay)
	{
		Vector3 position = enemy.transform.position;
		NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
		if (agent == null || !agent.isOnNavMesh || agent.velocity.sqrMagnitude < 0.01f) return position;

		float travel = agent.velocity.magnitude * delay;
		if (!agent.pathPending) travel = Mathf.Min(travel, Mathf.Max(0, agent.remainingDistance - agent.stoppingDistance));
		return position + agent.velocity.normalized * travel;
	}

	/// <summary>
	/// Point à viser pour que la grenade de shooter tombe là où l'ennemi sera à l'arrivée (rotation de la tourelle + 4 s de vol).
	/// </summary>
	public Transform GetAimPoint(Turret shooter, ArmyElement enemy)
	{
		Transform aimPoint;
		if (!m_AimPoints.TryGetValue(shooter, out aimPoint) || aimPoint == null)
		{
			aimPoint = new GameObject("AimPoint " + shooter.name).transform;
			aimPoint.SetParent(transform);
			m_AimPoints[shooter] = aimPoint;
		}

		Vector3 toEnemy = enemy.transform.position - shooter.m_TurretHead.position;
		float rotationDuration = Vector3.Angle(shooter.m_TurretHead.forward, toEnemy) / shooter.m_RotationSpeed;
		aimPoint.position = PredictPosition(enemy, rotationDuration + k_RocketTravelDuration);
		return aimPoint;
	}

	/// <summary>
	/// Une tourelle annonce qu'elle va tirer sur une cible fixe : les tourelles suivantes en tiendront compte (pas de sur-ciblage).
	/// La réservation dure le temps de rotation de la tourelle + le temps de vol de la grenade.
	/// </summary>
	public void ReserveRocketShot(Turret shooter, ArmyElement target)
	{
		Vector3 toTarget = target.transform.position - shooter.m_TurretHead.position;
		float rotationDuration = Vector3.Angle(shooter.m_TurretHead.forward, toTarget) / shooter.m_RotationSpeed;

		List<ShotReservation> reservations;
		if (!m_Reservations.TryGetValue(target, out reservations))
		{
			reservations = new List<ShotReservation>();
			m_Reservations[target] = reservations;
		}
		reservations.Add(new ShotReservation { Damage = k_RocketDamage, ExpireTime = Time.time + rotationDuration + k_RocketTravelDuration });
	}

	public override void ArmyElementHasBeenKilled(GameObject go)
	{
		base.ArmyElementHasBeenKilled(go);
		if (m_ArmyElements.Count == 0)
		{
			GUIUtility.systemCopyBuffer = "0\t" +((int)Timer.Value).ToString()+"\t0\t0\t0";
		}
	}
	public void GreenArmyIsDead(string deadArmyTag)
    {
        int nDrones = 0, nTurrets = 0, health = 0;
        ComputeStatistics(ref nDrones, ref nTurrets, ref health);
		GUIUtility.systemCopyBuffer = "1\t" + ((int)Timer.Value).ToString() + "\t"+nDrones.ToString()+"\t"+nTurrets.ToString()+"\t"+health.ToString();
		
		RefreshHudDisplay(); //pour une dernière mise à jour en cas de victoire
	}

}