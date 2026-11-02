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