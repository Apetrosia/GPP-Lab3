using UnityEngine;

// Пони плавно идёт за шариком и поворачивается в сторону движения.
public class PonyFollow : MonoBehaviour
{
    public Transform target;
    public float stopDistance = 1.6f;

    [Header("Скорость")]
    [SerializeField, Tooltip("Максимальная скорость пони, м/с")]
    float maxSpeed = 0.6f;
    [SerializeField, Range(0.1f, 3f)]
    float accelerationFactor = 1.5f;

    public float turnSpeed = 5f;

    // Для ручной регулировки из кода/редактора.
    public float MaxSpeed
    {
        get { return maxSpeed; }
        set { maxSpeed = value; }
    }

    public Vector3 Velocity { get; private set; }
    // Скорость поворота вокруг Y, градусы/с (нужна походке на поворотах).
    public float YawRate { get; private set; }

    Vector3 lastPosition;
    float lastYaw;

    void Start()
    {
        lastPosition = transform.position;
        lastYaw = transform.eulerAngles.y;
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
                float moveSpeed = maxSpeed * Mathf.Clamp01((distance - stopDistance) / accelerationFactor);
                transform.position += direction * (moveSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction),
                    turnSpeed * Time.deltaTime);
            }
        }

        Velocity = (transform.position - lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPosition = transform.position;

        float yaw = transform.eulerAngles.y;
        YawRate = Mathf.DeltaAngle(lastYaw, yaw) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastYaw = yaw;
    }
}
