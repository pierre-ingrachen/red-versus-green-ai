using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

// Tâches de l'armée ROUGE uniquement : la cible est désignée par l'ArmyManagerRed
// au lieu d'être tirée au hasard comme dans SelectEnemyDrone / SelectEnemyTurret.
public abstract class SelectFocusEnemy : Action
{
	protected IArmyElement m_ArmyElement;

	// shared : données partagées par tout l'arbre de décision
	public SharedTransform target;
	public SharedFloat minRadius;
	public SharedFloat maxRadius;

	public override void OnAwake()
	{
		m_ArmyElement = (IArmyElement)GetComponent(typeof(IArmyElement));
	}

	protected abstract Transform Select(ArmyManagerRed armyManager);

	public override TaskStatus OnUpdate()
	{
		if (m_ArmyElement.ArmyManager == null) return TaskStatus.Running; // la référence à l'armée n'a pas encore été injectée

		ArmyManagerRed armyManager = m_ArmyElement.ArmyManager as ArmyManagerRed;
		if (armyManager == null) return TaskStatus.Failure; // tâche réservée à l'armée rouge

		target.Value = Select(armyManager);

		return target.Value != null ? TaskStatus.Success : TaskStatus.Failure;
	}

	// la cible de la boucle précédente (null si elle est morte ou si c'était un point de visée)
	protected ArmyElement PreviousTarget()
	{
		return target.Value != null ? target.Value.GetComponent<ArmyElement>() : null;
	}

	protected static Transform TransformOf(GameObject go)
	{
		return go != null ? go.transform : null;
	}
}

[TaskCategory("MyTasks")]
[TaskDescription("Red army: select an enemy Drone. A drone is assigned to a group attacking the most advanced one and keeps it until it dies; a turret takes the nearest one, aiming where it is going to be")]
public class SelectFocusEnemyDrone : SelectFocusEnemy
{
	protected override Transform Select(ArmyManagerRed armyManager)
	{
		Turret shooter = GetComponent<Turret>();
		if (shooter == null)
			return TransformOf(armyManager.AssignTarget<Drone>(GetComponent<ArmyElement>(), minRadius.Value, maxRadius.Value, PreviousTarget()));

		// les drones bougent et la grenade met 4 s à tomber : on vise la position anticipée
		GameObject enemy = armyManager.GetNearestEnemy<Drone>(transform.position, minRadius.Value, maxRadius.Value, null);
		return enemy != null ? armyManager.GetAimPoint(shooter, enemy.GetComponent<ArmyElement>()) : null;
	}
}

[TaskCategory("MyTasks")]
[TaskDescription("Red army: select an enemy Turret. A drone is assigned to a group attacking the most advanced one and keeps it until it dies; a turret follows the focus fire of the ArmyManagerRed and reserves its rocket on the target")]
public class SelectFocusEnemyTurret : SelectFocusEnemy
{
	protected override Transform Select(ArmyManagerRed armyManager)
	{
		Turret shooter = GetComponent<Turret>();
		if (shooter == null)
			return TransformOf(armyManager.AssignTarget<Turret>(GetComponent<ArmyElement>(), minRadius.Value, maxRadius.Value, PreviousTarget()));

		GameObject enemy = armyManager.GetFocusEnemy<Turret>(transform.position, minRadius.Value, maxRadius.Value);

		// une tourelle ennemie ne bouge pas : la grenade est sûre de toucher, les tourelles suivantes peuvent viser autre chose quand les dégâts réservés suffisent à la détruire
		if (enemy != null) armyManager.ReserveRocketShot(shooter, enemy.GetComponent<ArmyElement>());

		return TransformOf(enemy);
	}
}
