using UnityEngine;

// Аналитическая 2-bone IK по «видимому» суставу ноги.
// Изгиб считается по предпоследней кости цепи (bones[Length-2]): у лошади это
// скакательный сустав сзади и запястье спереди, а не бедро/локоть. Промежуточные
// кости сохраняют свою позу, поэтому вся нижняя часть ноги поворачивается как
// единое целое и копыто реально достаёт до земли.
// Порядок выполнения: решаем ПОСЛЕ ProceduralLegs, который двигает таргеты.
[DefaultExecutionOrder(100)]
public class LimbIK : MonoBehaviour
{
    public Transform target;
    public Transform[] bones;    // [0]=плечо/бедро ... [n-2]=сустав изгиба
    public Transform endBone;    // само копыто
    // Направление сгиба в LOCAL пространстве пони-корня:
    //   Vector3.forward  → передние ноги (запястье вперёд)
    //   Vector3.back     → задние ноги (скакательный сустав назад)
    public Vector3 poleLocal = Vector3.forward;
    // Держим копыто параллельно земле, иначе оно клюёт носком на переносе.
    public bool keepFootFlat = true;

    [HideInInspector] public float upperLen; // bones[0] → сустав
    [HideInInspector] public float lowerLen; // сустав → endBone (низ жёсткий)

    int joint = 1;
    Quaternion restRootRot;
    Quaternion restFootLocal;

    void Awake()
    {
        if (bones == null || bones.Length < 2 || endBone == null) return;

        joint = Mathf.Max(1, bones.Length - 2);

        upperLen = Vector3.Distance(bones[0].position, bones[joint].position);
        lowerLen = Vector3.Distance(bones[joint].position, endBone.position);

        restRootRot = transform.rotation;
        restFootLocal = Quaternion.Inverse(restRootRot) * endBone.rotation;
    }

    void LateUpdate()
    {
        if (target == null || bones == null || bones.Length < 2 || endBone == null) return;

        Transform jointBone = bones[joint];
        Vector3 root = bones[0].position;
        Vector3 goal = target.position;

        Vector3 toGoal = goal - root;
        if (toGoal.sqrMagnitude < 1e-8f) return;
        Vector3 toGoalN = toGoal.normalized;

        float dist = Mathf.Clamp(
            toGoal.magnitude,
            Mathf.Abs(upperLen - lowerLen) + 0.001f,
            (upperLen + lowerLen) * 0.999f);

        // Pole в мировом пространстве.
        Vector3 pole = transform.TransformDirection(poleLocal).normalized;

        // Угол при плечевом суставе (теорема косинусов).
        float cosA = Mathf.Clamp(
            (upperLen * upperLen + dist * dist - lowerLen * lowerLen)
                / (2f * upperLen * dist), -1f, 1f);
        float angleA = Mathf.Acos(cosA) * Mathf.Rad2Deg;

        // Компонента pole, перпендикулярная toGoal.
        Vector3 polePerp = pole - Vector3.Dot(pole, toGoalN) * toGoalN;
        if (polePerp.sqrMagnitude < 1e-6f)
            polePerp = Vector3.up - Vector3.Dot(Vector3.up, toGoalN) * toGoalN;
        if (polePerp.sqrMagnitude < 1e-6f)
            polePerp = Vector3.right;
        polePerp = polePerp.normalized;

        // Позиция сустава: отклоняем toGoalN на angleA в сторону pole.
        Vector3 axis = Vector3.Cross(toGoalN, polePerp).normalized;
        Vector3 jointPos = root + Quaternion.AngleAxis(angleA, axis) * toGoalN * upperLen;

        // Поворачиваем bones[0]: текущее направление к суставу → к jointPos.
        Vector3 curUpper = jointBone.position - bones[0].position;
        Vector3 wantUpper = jointPos - bones[0].position;
        if (curUpper.sqrMagnitude > 1e-6f && wantUpper.sqrMagnitude > 1e-6f)
            bones[0].rotation = Quaternion.FromToRotation(curUpper, wantUpper) * bones[0].rotation;

        // Поворачиваем сам сустав: направление на копыто → на цель.
        Vector3 curLower = endBone.position - jointBone.position;
        Vector3 wantLower = goal - jointBone.position;
        if (curLower.sqrMagnitude > 1e-6f && wantLower.sqrMagnitude > 1e-6f)
            jointBone.rotation = Quaternion.FromToRotation(curLower, wantLower) * jointBone.rotation;

        if (keepFootFlat)
            endBone.rotation = transform.rotation * restFootLocal;
    }
}
