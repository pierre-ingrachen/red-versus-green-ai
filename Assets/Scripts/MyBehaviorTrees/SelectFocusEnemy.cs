using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

// Tâches de l'armée ROUGE uniquement : la cible est désignée par l'ArmyManagerRed (focus fire)
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

	protected abstract GameObject Select(ArmyManagerRed armyManager);

	public override TaskStatus OnUpdate()
	{
		if (m_ArmyElement.ArmyManager == null) return TaskStatus.Running; // la référence à l'armée n'a pas encore été injectée

		ArmyManagerRed armyManager = m_ArmyElement.ArmyManager as ArmyManagerRed;
		if (armyManager == null) return TaskStatus.Failure; // tâche réservée à l'armée rouge

		GameObject enemy = Select(armyManager);
		target.Value = enemy != null ? enemy.transform : null;

		return target.Value != null ? TaskStatus.Success : TaskStatus.Failure;
	}
}

[TaskCategory("MyTasks")]
[TaskDescription("Red army: select the enemy Drone designated by the focus fire of the ArmyManagerRed")]
public class SelectFocusEnemyDrone : SelectFocusEnemy
{
	protected override GameObject Select(ArmyManagerRed armyManager)
	{
		return armyManager.GetFocusEnemy<Drone>(transform.position, minRadius.Value, maxRadius.Value);
	}
}

[TaskCategory("MyTasks")]
[TaskDescription("Red army: select the enemy Turret designated by the focus fire of the ArmyManagerRed. A shooting turret reserves its rocket on the target")]
public class SelectFocusEnemyTurret : SelectFocusEnemy
{
	protected override GameObject Select(ArmyManagerRed armyManager)
	{
		GameObject enemy = armyManager.GetFocusEnemy<Turret>(transform.position, minRadius.Value, maxRadius.Value);

		// une tourelle ennemie ne bouge pas : la grenade est sûre de toucher, les tourelles suivantes peuvent viser autre chose quand les dégâts réservés suffisent à la détruire
		Turret shooter = GetComponent<Turret>();
		if (enemy != null && shooter != null) armyManager.ReserveRocketShot(shooter, enemy.GetComponent<ArmyElement>());

		return enemy;
	}
}
