using UnityEngine;
using UnityEngine.AI;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

// Les missiles sont rectilignes et mettent ~0,4 s à parcourir 6 m : un drone qui ne s'arrête jamais et tourne autour de sa cible
// (au lieu de rester immobile comme les drones verts) est très difficile à toucher.
// La vitesse et la vitesse angulaire de l'agent ne sont pas modifiées : elles ont été fixées par MySeek (Annexe A).
public abstract class StrafeMovement : Action
{
	public SharedTransform target;

	protected NavMeshAgent m_Agent;
	ArmyElement m_ArmyElement;
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
		m_Agent.isStopped = false;
		m_Agent.updateRotation = false; // le drone avance de côté en restant tourné vers sa cible
		m_Direction = Random.value < .5f ? -1 : 1;
		m_NextFlip = Time.time + Random.Range(0.8f, 1.6f);
		m_NextRepath = 0;
	}

	// avance sur un cercle autour de la cible, en changeant de sens de temps en temps pour ne pas être prévisible
	protected void Strafe()
	{
		if (Dodge()) return;
		if (!target.Value) return;

		if (Time.time >= m_NextFlip)
		{
			m_Direction = -m_Direction;
			m_NextFlip = Time.time + Random.Range(0.8f, 1.6f);
		}

		if (Time.time < m_NextRepath) return;
		m_NextRepath = Time.time + 0.2f;

		Vector3 fromTarget = Vector3.ProjectOnPlane(transform.position - target.Value.position, Vector3.up);
		float radius = Mathf.Clamp(fromTarget.magnitude, 6f, 12f);
		Vector3 next = target.Value.position + Quaternion.AngleAxis(m_Direction * 40f, Vector3.up) * fromTarget.normalized * radius;

		NavMeshHit hit;
		if (NavMesh.SamplePosition(next, out hit, 3f, NavMesh.AllAreas)) m_Agent.SetDestination(hit.position);
	}

	// une grenade ennemie va tomber sur nous : on s'écarte de la zone d'impact (le chef d'état-major voit sa trajectoire)
	bool Dodge()
	{
		ArmyManagerRed armyManager = m_ArmyElement != null ? m_ArmyElement.ArmyManager as ArmyManagerRed : null;
		Vector3 impact;
		if (armyManager == null || !armyManager.TryGetIncomingImpact(transform.position, out impact)) return false;

		if (Time.time >= m_NextRepath)
		{
			m_NextRepath = Time.time + 0.1f;
			NavMeshHit hit;
			if (NavMesh.SamplePosition(armyManager.EscapePoint(transform.position, impact), out hit, 3f, NavMesh.AllAreas)) m_Agent.SetDestination(hit.position);
		}
		return true;
	}
}
