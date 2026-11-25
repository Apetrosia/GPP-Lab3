using UnityEngine;

// Пони плавно идёт за шариком и поворачивается в сторону движения.
public class PonyFollow : MonoBehaviour
{
    public Transform target;
    public float stopDistance = 1.6f;
    public float speed = 1.4f;
    public float turnSpeed = 5f;

    public Vector3 Velocity { get; private set; }

    Vector3 lastPosition;

    void Start()
    {
        lastPosition = transform.position;
    }

    void Update()
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance > 0.001f)
        {
            Vector3 direction = toTarget / distance;
            if (distance > stopDistance)
            {
                float moveSpeed = speed * Mathf.Clamp01((distance - stopDistance) / 1.5f);
                transform.position += direction * (moveSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction),
                    turnSpeed * Time.deltaTime);
            }
        }

        Velocity = (transform.position - lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPosition = transform.position;
    }
}
