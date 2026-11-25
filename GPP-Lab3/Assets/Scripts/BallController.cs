using UnityEngine;
using UnityEngine.InputSystem;

// Шарик, которым управляет игрок. Пони бежит за ним.
public class BallController : MonoBehaviour
{
    public float speed = 2f;
    public float radius = 0.4f;

    void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;

        Vector3 dir = new Vector3(
            (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0),
            0,
            (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));

        if (dir.sqrMagnitude > 1f) dir.Normalize();

        Vector3 move = dir * speed * Time.deltaTime;
        transform.position += move;
        transform.position = new Vector3(transform.position.x, radius, transform.position.z);

        // Небольшой визуальный катящийся эффект.
        if (move.sqrMagnitude > 0.000001f)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, move.normalized);
            transform.Rotate(axis, move.magnitude / radius * Mathf.Rad2Deg, Space.World);
        }
    }
}
