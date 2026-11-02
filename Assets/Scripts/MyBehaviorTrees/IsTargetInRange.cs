using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("MyTasks")]
[TaskDescription("Success if the target is still alive and closer than maxDistance (no need to move closer to shoot)")]
public class IsTargetInRange : Conditional
{
	public SharedTransform target;
	public SharedFloat maxDistance;

	public override TaskStatus OnUpdate()
	{
		if (!target.Value) return TaskStatus.Failure;

		return Vector3.Distance(transform.position, target.Value.position) <= maxDistance.Value ? TaskStatus.Success : TaskStatus.Failure;
	}
}
