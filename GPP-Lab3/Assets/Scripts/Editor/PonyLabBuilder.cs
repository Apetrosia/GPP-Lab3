using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Собирает сцену с пони, шариком и целями для лап.
public static class PonyLabBuilder
{
    const string ModelPath = "Assets/Models/queen-chrysalis-from-mlp/source/Chrysalis.fbx";
    const string ScenePath = "Assets/Scenes/PonyLab.unity";
    const string MaterialFolder = "Assets/Materials";

    [MenuItem("Tools/Pony Lab/Build Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Земля.
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = Vector3.one * 6f;
        ground.GetComponent<Renderer>().sharedMaterial = MakeGroundMaterial();

        // Шарик.
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Ball";
        ball.transform.position = new Vector3(0f, 0.4f, 4f);
        ball.transform.localScale = Vector3.one * 0.8f;
        ball.GetComponent<Renderer>().sharedMaterial = MakeMaterial("BallMat", new Color(1f, 0.45f, 0.12f));
        var ballController = ball.AddComponent<BallController>();
        ballController.radius = 0.4f;
        ballController.speed = 2f;

        // Пони.
        var pony = new GameObject("Pony");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        model.name = "Body";
        model.transform.SetParent(pony.transform, false);
        model.transform.localScale = Vector3.one * 0.35f;

        var follow = pony.AddComponent<PonyFollow>();
        follow.target = ball.transform;
        follow.MaxSpeed = 0.6f;
        follow.stopDistance = 1.4f;

        // Цели копыт.
        var targets = new GameObject("IK Targets");
        float[] phases = { 0f, 0.25f, 0.5f, 0.75f }; // LH LF RH RF — шаг в 4 такта
        string[] hooves = { "r_hoof.L", "f_hoof.L", "r_hoof.R", "f_hoof.R" };
        string[][] chains =
        {
            new[] { "thigh.L", "lower_leg.L", "hind_foot.L", "r_toe.L" },
            new[] { "upper_arm.L", "forearm.L", "forefoot.L", "f_toe.L" },
            new[] { "thigh.R", "lower_leg.R", "hind_foot.R", "r_toe.R" },
            new[] { "upper_arm.R", "forearm.R", "forefoot.R", "f_toe.R" }
        };

        var legs = new ProceduralLegs.Leg[4];
        for (int i = 0; i < 4; i++)
        {
            Transform hoof = Find(model.transform, hooves[i]);
            var target = new GameObject("Target_" + hooves[i]).transform;
            target.SetParent(targets.transform);
            target.position = hoof.position;

            // Pole-вектор: передние ноги (i=1,3) — колено вперёд;
            // задние (i=0,2) — скакательный сустав назад.
            // Только Z-компонента — стабильно, без бокового скручивания.
            Vector3[] poles = {
                Vector3.back,    // HindL
                Vector3.forward, // FrontL
                Vector3.back,    // HindR
                Vector3.forward  // FrontR
            };
            // Задние — два сустава (бедро + скакательный), низ жёсткий.
            // Передние — три: плечо, запястье и копытный сустав.
            bool[] useLast = { false, true, false, true };
            var ik = pony.AddComponent<LimbIK>();
            ik.target = target;
            ik.endBone = hoof;
            ik.poleLocal = poles[i];
            ik.useLastJoint = useLast[i];
            ik.lastJointShare = 0.3f;
            ik.bones = new Transform[chains[i].Length];
            for (int b = 0; b < chains[i].Length; b++)
                ik.bones[b] = Find(model.transform, chains[i][b]);

            legs[i] = new ProceduralLegs.Leg
            {
                hoof = hoof,
                ik = ik,
                phase = phases[i]
            };
        }

        var gait = pony.AddComponent<ProceduralLegs>();
        gait.pony = follow;
        gait.body = model.transform;
        gait.legs = legs;
        gait.bodyDrop = 0.024f;
        gait.bodyBobAmp = 0.006f;
        gait.strideLen = 0.40f;
        gait.swingFraction = 0.32f;
        gait.stepHeight = 0.06f;

        var motion = model.AddComponent<PonyBodyMotion>();
        motion.tail = FindAll(model.transform, "tail.");
        motion.neck = FindAll(model.transform, "neck.");
        motion.wings = FindAll(model.transform, "Wings.");

        // Свет и камера.
        var light = Object.FindAnyObjectByType<Light>();
        light.intensity = 2.4f;
        light.color = new Color(1f, 0.96f, 0.9f);

        var camera = Camera.main;
        camera.transform.position = new Vector3(2.2f, 2.6f, -6f);
        var cameraFollow = camera.gameObject.AddComponent<CameraFollow>();
        cameraFollow.target = ball.transform;
        cameraFollow.pony = pony.transform;

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Pony lab scene built: " + ScenePath);
    }

    static Material MakeGroundMaterial()
    {
        var material = MakeMaterial("GroundMat", new Color(0.34f, 0.40f, 0.32f));

        string path = MaterialFolder + "/GroundChecker.asset";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            texture = new Texture2D(64, 64);
            texture.name = "GroundChecker";
            var a = new Color(0.30f, 0.35f, 0.28f);
            var b = new Color(0.36f, 0.42f, 0.33f);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    texture.SetPixel(x, y, ((x / 8) + (y / 8)) % 2 == 0 ? a : b);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
        }

        material.mainTexture = texture;
        material.mainTextureScale = new Vector2(20f, 20f);
        material.SetTexture("_BaseMap", texture);
        material.SetTextureScale("_BaseMap", new Vector2(20f, 20f));
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material MakeMaterial(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets", "Materials");

        string path = MaterialFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        Debug.LogWarning("Bone not found: " + name);
        return null;
    }

    static Transform[] FindAll(Transform root, string prefix)
    {
        var list = new System.Collections.Generic.List<Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith(prefix)) list.Add(t);
        return list.ToArray();
    }
}
