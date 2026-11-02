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
	float m_Direction;
	float m_NextFlip;
	float m_NextRepath;

	public override void OnAwake()
	{
		m_Agent = GetComponent<NavMeshAgent>();
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
}
