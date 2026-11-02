using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("MyTasks")]
[TaskDescription("Keep circling around the target (never returns). Meant to run in parallel with a Wait, so that the drone does not stand still")]
public class StrafeAround : StrafeMovement
{
	public override TaskStatus OnUpdate()
	{
		Strafe();
		return TaskStatus.Running;
	}

	public override void OnEnd()
	{
		m_Agent.updateRotation = true;
	}
}
