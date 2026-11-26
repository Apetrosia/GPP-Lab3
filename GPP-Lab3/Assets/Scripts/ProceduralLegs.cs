using UnityEngine;

// Процедурная походка лошади — шаг в четыре такта: ЛЗ → ЛП → ПЗ → ПП.
// Пока нога в опоре, копыто стоит в мировых координатах (не скользит), а в свой
// такт переносится по дуге вперёд с запасом на пол-опоры. Корпус проседает на
// bodyDrop (даёт ногам запас для сгиба) и покачивается поверх бэйкнутой позы FBX.
// Таргеты двигаются здесь, IK решает позже (см. DefaultExecutionOrder в LimbIK).
[DefaultExecutionOrder(-5)]
public class ProceduralLegs : MonoBehaviour
{
    [System.Serializable]
    public class Leg
    {
        public Transform hoof;
        public LimbIK ik;
        [Tooltip("Такт в цикле: ЛЗ 0, ЛП 0.25, ПЗ 0.5, ПП 0.75")]
        public float phase;

        [HideInInspector] public float groundY;      // Y копыта в T-позе (уровень земли)
        [HideInInspector] public Vector3 footWorld;  // текущая заземлённая точка
        [HideInInspector] public Vector3 stepFrom;
        [HideInInspector] public Vector3 stepTo;
        [HideInInspector] public float stepT = 1f;
        [HideInInspector] public bool stepping;
        [HideInInspector] public float reach;        // макс. вынос копыта от точки под бедром
        [HideInInspector] public float prevPhase;
    }

    public PonyFollow pony;
    public Transform body;
    public Leg[] legs = new Leg[4];

    [Header("Body")]
    public float bodyDrop = 0.02f;      // просадка корпуса, чтобы ноги могли сгибаться
    public float bodyBobAmp   = 0.012f; // корпус выше всего в середине опоры каждой ноги
    public float bodyRollAmp  = 1.0f;
    public float bodyPitchAmp = 0.6f;

    [Header("Stride")]
    [Tooltip("Сколько тело проходит за один цикл шага")]
    public float strideLen     = 0.44f;
    [Range(0.1f, 0.45f)] public float swingFraction = 0.32f; // доля цикла в переносе
    public float stepHeight    = 0.06f;

    float gaitCycle;
    Vector3 bodyBase;
    Quaternion bodyRotStart;
    float effectiveStride = 0.44f;
    float bobPhase;
    bool ready;

    void Start()
    {
        if (body != null)
        {
            bodyBase = body.localPosition;
            bodyRotStart = body.localRotation;
        }

        // Сначала запоминаем уровень земли ДО опускания корпуса.
        for (int i = 0; i < legs.Length; i++)
        {
            Leg leg = legs[i];
            if (!Valid(leg)) continue;

            leg.groundY = leg.hoof.position.y;
            Vector3 p = leg.hoof.position;
            p.y = leg.groundY;
            leg.footWorld = p;
            leg.ik.target.position = p;
            leg.stepT = 1f;
            leg.stepping = false;
            leg.prevPhase = Mathf.Repeat(leg.phase, 1f);
        }

        // Фаза покачивания: корпус должен быть выше всего в середине опоры.
        foreach (Leg leg in legs)
        {
            if (!Valid(leg)) continue;
            float midStance = leg.phase + (1f + swingFraction) * 0.5f;
            bobPhase = Mathf.Repeat(4f * midStance, 1f);
            break;
        }

        // Теперь опускаем корпус — ноги получают запас для сгиба.
        if (body != null)
            body.localPosition = bodyBase - Vector3.up * bodyDrop;

        // Насколько далеко копыто может уйти в сторону от точки под бедром,
        // оставаясь достижимым: радиус сферы досягаемости ноги на уровне земли.
        // Считаем по самому низкому положению корпуса (просадка + покачивание).
        float minReach = float.MaxValue;
        foreach (Leg leg in legs)
        {
            if (!Valid(leg)) continue;
            float chain = leg.ik.chainLen * 0.98f;
            float hipHeight = Mathf.Max(0.01f,
                leg.ik.bones[0].position.y - leg.groundY - bodyBobAmp);
            float r2 = chain * chain - hipHeight * hipHeight;
            leg.reach = Mathf.Max(0.06f, r2 > 0.0001f ? Mathf.Sqrt(r2) : 0.06f);
            minReach = Mathf.Min(minReach, leg.reach);
        }
        if (minReach == float.MaxValue) minReach = 0.12f;

        // Длина шага не должна выводить самую короткую ногу за пределы досягаемости.
        float maxStride = 2f * minReach * 0.88f / Mathf.Max(0.2f, 1f - swingFraction);
        effectiveStride = Mathf.Min(strideLen, maxStride);

        ready = true;
    }

    static bool Valid(Leg leg)
    {
        return leg != null && leg.hoof != null && leg.ik != null
            && leg.ik.target != null && leg.ik.bones != null && leg.ik.bones.Length >= 2;
    }

    void LateUpdate()
    {
        if (!ready || pony == null) return;

        Vector3 velocity = pony.Velocity;
        velocity.y = 0f;
        float speed = velocity.magnitude;
        bool moving = speed > 0.02f;

        // Каденция: один цикл на effectiveStride пути. Стоя — цикл замирает.
        float rate = 0f;
        if (moving)
        {
            rate = Mathf.Clamp(speed / Mathf.Max(effectiveStride, 0.05f), 0.5f, 4f);
            gaitCycle += rate * Time.deltaTime;
        }

        // Корпус: просадка + покачивание поверх бэйкнутого FBX-поворота.
        float moving01 = Mathf.Clamp01(speed / 0.25f);
        float bob = Mathf.Cos((gaitCycle * 4f - bobPhase) * Mathf.PI * 2f)
            * bodyBobAmp * moving01;
        float roll  = Mathf.Sin(gaitCycle * Mathf.PI * 2f) * bodyRollAmp * moving01;
        float pitch = Mathf.Sin(gaitCycle * Mathf.PI * 4f + Mathf.PI * 0.5f) * bodyPitchAmp * moving01;
        if (body != null)
        {
            body.localPosition = bodyBase - Vector3.up * bodyDrop + Vector3.up * bob;
            body.localRotation = bodyRotStart * Quaternion.Euler(pitch, 0f, roll);
        }

        float swingTime  = rate > 0.0001f ? swingFraction / rate : 0.25f;
        float stanceTime = rate > 0.0001f ? (1f - swingFraction) / rate : 0f;

        for (int i = 0; i < legs.Length; i++)
        {
            Leg leg = legs[i];
            if (!Valid(leg)) continue;

            // Точка «отдыха» — на земле под бедром/плечом.
            Vector3 hip = leg.ik.bones[0].position;
            Vector3 rest = new Vector3(hip.x, leg.groundY, hip.z);

            float p = Mathf.Repeat(gaitCycle - leg.phase, 1f);
            bool windowStart = p < swingFraction && leg.prevPhase > p; // вошли в свой такт
            leg.prevPhase = p;

            if (!leg.stepping && moving && (windowStart || Overdue(leg, rest)))
                BeginStep(leg, rest, velocity, swingTime, stanceTime);

            Vector3 target;

            if (leg.stepping)
            {
                float stepRate = rate > 0.0001f ? rate : 0.8f;
                leg.stepT += Time.deltaTime * stepRate / Mathf.Max(swingFraction, 0.01f);

                if (leg.stepT >= 1f)
                {
                    leg.stepT = 1f;
                    leg.stepping = false;
                    leg.footWorld = leg.stepTo;
                    target = leg.footWorld;
                }
                else
                {
                    float t  = leg.stepT;
                    float ts = t * t * (3f - 2f * t);
                    target = Vector3.Lerp(leg.stepFrom, leg.stepTo, ts);
                    target.y = leg.groundY + Mathf.Sin(Mathf.PI * t) * stepHeight;
                }
            }
            else
            {
                // Стоим — плавно подтягиваем копыто под бедро.
                if (!moving)
                {
                    leg.footWorld = Vector3.Lerp(leg.footWorld, rest,
                        1f - Mathf.Exp(-6f * Time.deltaTime));
                    leg.footWorld.y = leg.groundY;
                }
                target = leg.footWorld;
            }

            leg.ik.target.position = target;
        }
    }

    // Копыто уже не достаёт до земли — шагаем вне очереди.
    static bool Overdue(Leg leg, Vector3 rest)
    {
        Vector3 d = leg.footWorld - rest;
        d.y = 0f;
        return d.magnitude > leg.reach;
    }

    void BeginStep(Leg leg, Vector3 rest, Vector3 velocity, float swingTime, float stanceTime)
    {
        leg.stepping = true;
        leg.stepT = 0f;
        leg.stepFrom = leg.footWorld;

        // Копыто ставится на пол-опоры впереди бедра в момент приземления —
        // тогда середина опоры приходится ровно под бедром, а вынос за пределы
        // досягаемости одинаков вперёд и назад.
        Vector3 landingHip = rest + velocity * swingTime;
        Vector3 offset = velocity * (stanceTime * 0.5f);

        float max = leg.reach * 0.95f;
        if (offset.magnitude > max)
            offset = offset.normalized * max;

        leg.stepTo = landingHip + offset;
        leg.stepTo.y = leg.groundY;
    }
}
