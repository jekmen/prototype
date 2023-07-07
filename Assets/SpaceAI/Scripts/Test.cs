using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    public Transform target;

    public Transform[] baces;

    void Update()
    {
        RotateBase();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        foreach (var item in baces)
        {
            Gizmos.DrawLine(item.position, item.position + item.forward * 50);
        }
    }

    private void RotateBase()
    {
        foreach (var turrBase in baces)
        {
            Vector3 directionToTarget = target.position - turrBase.position;
            directionToTarget.y = 0.0F;

            Vector3 clampedLocalVec2Target = directionToTarget;

            if (true)
            {
                if (directionToTarget.x >= 0.0f)
                    clampedLocalVec2Target = Vector3.RotateTowards(Vector3.forward, directionToTarget, Mathf.Deg2Rad * 35, float.MaxValue);
                else
                    clampedLocalVec2Target = Vector3.RotateTowards(Vector3.forward, directionToTarget, Mathf.Deg2Rad * 35, float.MaxValue);
            }

            Quaternion targetRotation = Quaternion.LookRotation(clampedLocalVec2Target);

            turrBase.rotation = Quaternion.Slerp(turrBase.rotation, targetRotation, 25 * Time.deltaTime);
        }
    }
}
