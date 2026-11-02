using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("MyTasks")]
[TaskDescription("Circle around the target while rotating towards where it is going to be. Success when aligned, failure if the target dies, is too far or takes too long")]
public class StrafeAim : StrafeMovement
{
	[BehaviorDesigner.Runtime.Tasks.Tooltip("The Rotation slerping coef (same as My Slerp Rotate Around Y)")]
	public SharedFloat slerpCoef;
	[BehaviorDesigner.Runtime.Tasks.Tooltip("The Arrival Angle magnitude (same as My Slerp Rotate Around Y)")]
	public SharedFloat arrivalAngle;
	public SharedFloat maxDistance;
	public SharedFloat timeout;

	float m_StartTime;

	public override void OnStart()
	{
		base.OnStart();
		m_StartTime = Time.time;
	}

	public override TaskStatus OnUpdate()
	{
		if (!target.Value) return TaskStatus.Failure;
		if (Vector3.Distance(transform.position, target.Value.position) > maxDistance.Value) return TaskStatus.Failure;
		if (Time.time - m_StartTime > timeout.Value) return TaskStatus.Failure;

		Strafe();

		Vector3 aimPosition = DroneAim.PredictPosition(transform, target.Value);
		Vector3 dir = Vector3.ProjectOnPlane(aimPosition - transform.position, Vector3.up).normalized;
		if (dir.sqrMagnitude < 0.001f) return TaskStatus.Running;

		Quaternion qEndOrient = Quaternion.LookRotation(dir);
		transform.rotation = Quaternion.Slerp(transform.rotation, qEndOrient, Time.deltaTime * slerpCoef.Value);
		if (Quaternion.Angle(transform.rotation, qEndOrient) < arrivalAngle.Value) return TaskStatus.Success;

		return TaskStatus.Running;
	}
}
