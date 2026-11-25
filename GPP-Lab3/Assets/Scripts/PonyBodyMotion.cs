using UnityEngine;

// Вторичная анимация: хвост, шея и крылья слегка качаются сами по себе.
public class PonyBodyMotion : MonoBehaviour
{
    public Transform[] tail;
    public Transform[] neck;
    public Transform[] wings;

    Quaternion[] tailStart;
    Quaternion[] neckStart;
    Quaternion[] wingStart;

    void Start()
    {
        tailStart = Save(tail);
        neckStart = Save(neck);
        wingStart = Save(wings);
    }

    static Quaternion[] Save(Transform[] items)
    {
        var result = new Quaternion[items.Length];
        for (int i = 0; i < items.Length; i++)
            result[i] = items[i].localRotation;
        return result;
    }

    void LateUpdate()
    {
        float t = Time.time;

        for (int i = 0; i < tail.Length; i++)
        {
            float wave = Mathf.Sin(t * 2.2f + i * 0.7f) * (6f - i);
            tail[i].localRotation = tailStart[i] * Quaternion.Euler(0f, 0f, wave);
        }

        for (int i = 0; i < neck.Length; i++)
        {
            float wave = Mathf.Sin(t * 1.6f + i * 0.5f) * 1.5f;
            neck[i].localRotation = neckStart[i] * Quaternion.Euler(wave, 0f, 0f);
        }

        for (int i = 0; i < wings.Length; i++)
        {
            float flap = Mathf.Sin(t * 2.8f + i * Mathf.PI) * 4f;
            wings[i].localRotation = wingStart[i] * Quaternion.Euler(0f, 0f, flap);
        }
    }
}
