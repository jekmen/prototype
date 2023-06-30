using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyTest : MonoBehaviour
{
    public Transform target;
    public float speed = 10f;
    public float ratationSpeed;
    public Rigidbody rb;

    private void FixedUpdate()
    {
        Vector3 relativePoint = transform.InverseTransformPoint(target.position).normalized;

        var rotSpeed = ratationSpeed * Time.deltaTime *1000;

        var shipRotation = Quaternion.Euler(-relativePoint.y * Time.deltaTime * rotSpeed, 0, -relativePoint.x * Time.deltaTime * rotSpeed);

        rb.rotation *= shipRotation;
        rb.velocity = transform.forward * speed;
    }
}