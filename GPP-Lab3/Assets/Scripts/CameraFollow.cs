using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Transform pony;
    public Vector3 offset = new Vector3(2.2f, 2.6f, -6f);
    public float smooth = 4f;

    void LateUpdate()
    {
        Vector3 focus = (target.position + pony.position) * 0.5f;
        transform.position = Vector3.Lerp(transform.position, focus + offset, smooth * Time.deltaTime);
        transform.LookAt(focus + Vector3.up * 0.9f);
    }
}
