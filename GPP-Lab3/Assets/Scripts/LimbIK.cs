using UnityEngine;

// IK ноги лошади: три управляемых сустава — возле туловища, колено и копытный
// сустав. Цепь решается аналитически в плоскости (корень → цель, pole):
// корень отклоняется на угол a, колено и копытный сустав сгибаются на b0 и b1.
// Изгиб делится между ними по lastJointShare, поэтому нога складывается плавной
// дугой, а не ломается в одном месте.
// Порядок выполнения: решаем ПОСЛЕ ProceduralLegs, который двигает таргеты.
[DefaultExecutionOrder(100)]
public class LimbIK : MonoBehaviour
{
    public Transform target;
    public Transform[] bones;    // [0]=плечо/бедро, [n-2]=колено/скакательный, [n-1]=копытный сустав
    public Transform endBone;    // само копыто
    // Направление сгиба в LOCAL пространстве пони-корня:
    //   Vector3.forward  → передние ноги (запястье вперёд)
    //   Vector3.back     → задние ноги (скакательный сустав назад)
    public Vector3 poleLocal = Vector3.forward;
    // Сгибать ли самый нижний сустав (путовый). Если выключено — вся часть ноги
    // ниже колена/скакательного сустава работает как одна жёсткая кость.
    public bool useLastJoint = true;
    // Какая доля изгиба уходит в нижний сустав (0 — всё в колено, 1 — всё в копыто).
    [Range(0f, 1f)] public float lastJointShare = 0.5f;
    // Держим копыто параллельно земле, иначе оно клюёт носком на переносе.
    public bool keepFootFlat = true;

    [HideInInspector] public float upperLen; // bones[0] → колено
    [HideInInspector] public float midLen;   // колено → копытный сустав
    [HideInInspector] public float lowerLen; // копытный сустав → копыто
    [HideInInspector] public float chainLen; // вся нога целиком

    int jointA;   // колено
    int jointB;   // копытный сустав (-1, если цепь короткая)

    Quaternion restRootRot;
    Quaternion restFootLocal;

    void Awake()
    {
        if (bones == null || bones.Length < 2 || endBone == null) return;

        jointA = Mathf.Max(1, bones.Length - 2);
        jointB = (useLastJoint && bones.Length >= 3) ? bones.Length - 1 : -1;

        Transform lowerRoot = jointB > 0 ? bones[jointB] : bones[jointA];

        upperLen = Vector3.Distance(bones[0].position, bones[jointA].position);
        midLen = jointB > 0 ? Vector3.Distance(bones[jointA].position, bones[jointB].position) : 0f;
        lowerLen = Vector3.Distance(lowerRoot.position, endBone.position);
        chainLen = upperLen + midLen + lowerLen;

        restRootRot = transform.rotation;
        restFootLocal = Quaternion.Inverse(restRootRot) * endBone.rotation;
    }

    void LateUpdate()
    {
        if (target == null || bones == null || bones.Length < 2 || endBone == null) return;

        Vector3 root = bones[0].position;
        Vector3 toGoal = target.position - root;
        if (toGoal.sqrMagnitude < 1e-8f) return;

        Vector3 e1 = toGoal.normalized;
        Vector3 pole = transform.TransformDirection(poleLocal);
        Vector3 e2 = pole - Vector3.Dot(pole, e1) * e1;
        if (e2.sqrMagnitude < 1e-6f)
            e2 = Vector3.up - Vector3.Dot(Vector3.up, e1) * e1;
        if (e2.sqrMagnitude < 1e-6f)
            e2 = Vector3.right;
        e2.Normalize();

        if (jointB > 0) Solve3(root, e1, e2, toGoal.magnitude);
        else Solve2(root, e1, e2, toGoal.magnitude);

        if (keepFootFlat)
            endBone.rotation = transform.rotation * restFootLocal;
    }

    // Три сустава: бедро/плечо → колено → копыто.
    void Solve3(Vector3 root, Vector3 e1, Vector3 e2, float distance)
    {
        float min = Mathf.Abs(upperLen - midLen - lowerLen) + 0.001f;
        float d = Mathf.Clamp(distance, min, chainLen * 0.999f);

        float share = Mathf.Clamp01(lastJointShare);
        float bend = 0f;

        if (d < chainLen * 0.9995f)
        {
            // Общий изгиб ищем делением пополам: чем сильнее согнута нога,
            // тем меньше расстояние по хорде. Монотонно, поэтому бисекция.
            float lo = 0f, hi = Mathf.PI * 0.5f;
            for (int i = 0; i < 18; i++)
            {
                float m = (lo + hi) * 0.5f;
                if (AxialReach(m, share) > d) lo = m;
                else hi = m;
            }
            bend = (lo + hi) * 0.5f;
        }

        float b0 = (1f - share) * bend;
        float b1 = share * bend;
        float a = FirstAngle(b0, b1);

        Vector3 p1 = root + (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a)) * upperLen;
        Vector3 p2 = p1 + (e1 * Mathf.Cos(a + b0) + e2 * Mathf.Sin(a + b0)) * midLen;
        Vector3 p3 = p2 + (e1 * Mathf.Cos(a + b0 + b1) + e2 * Mathf.Sin(a + b0 + b1)) * lowerLen;

        Align(bones[0], bones[jointA], p1);
        Align(bones[jointA], bones[jointB], p2);
        Align(bones[jointB], endBone, p3);
    }

    // Две кости (запасной вариант, если в цепи меньше трёх суставов).
    void Solve2(Vector3 root, Vector3 e1, Vector3 e2, float distance)
    {
        Transform joint = bones[jointA];
        float d = Mathf.Clamp(distance,
            Mathf.Abs(upperLen - lowerLen) + 0.001f,
            (upperLen + lowerLen) * 0.999f);

        float cosA = Mathf.Clamp(
            (upperLen * upperLen + d * d - lowerLen * lowerLen)
                / (2f * upperLen * d), -1f, 1f);
        float a = Mathf.Acos(cosA);

        Vector3 p1 = root + (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a)) * upperLen;
        Vector3 p2 = root + e1 * d;

        Align(bones[0], joint, p1);
        Align(joint, endBone, p2);
    }

    // Угол первой кости от направления на цель, при котором поперечная
    // составляющая цепи равна нулю (копыто остаётся на оси цель-корень).
    float FirstAngle(float b0, float b1)
    {
        float lo = -Mathf.PI * 0.5f, hi = 0f;
        for (int i = 0; i < 18; i++)
        {
            float a = (lo + hi) * 0.5f;
            float f = upperLen * Mathf.Sin(a)
                    + midLen * Mathf.Sin(a + b0)
                    + lowerLen * Mathf.Sin(a + b0 + b1);
            if (f < 0f) lo = a;
            else hi = a;
        }
        return (lo + hi) * 0.5f;
    }

    // Расстояние по хорде при заданном общем изгибе.
    float AxialReach(float bend, float share)
    {
        float b0 = (1f - share) * bend;
        float b1 = share * bend;
        float a = FirstAngle(b0, b1);
        return upperLen * Mathf.Cos(a)
             + midLen * Mathf.Cos(a + b0)
             + lowerLen * Mathf.Cos(a + b0 + b1);
    }

    static void Align(Transform bone, Transform child, Vector3 point)
    {
        Vector3 cur = child.position - bone.position;
        Vector3 want = point - bone.position;
        if (cur.sqrMagnitude < 1e-8f || want.sqrMagnitude < 1e-8f) return;
        bone.rotation = Quaternion.FromToRotation(cur, want) * bone.rotation;
    }
}
