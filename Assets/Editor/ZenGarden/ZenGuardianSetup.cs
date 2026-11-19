using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using ZenGarden.NPC;

namespace ZenGarden.Editor
{
    public static class ZenGuardianSetup
    {
        private const string CharacterFbxPath = "Assets/Characters/ZenGuardian/kachujin_model.fbx";
        private const string AnimIdlePath = "Assets/Characters/ZenGuardian/kachujin_anim_idle.fbx";
        private const string AnimWavePath = "Assets/Characters/ZenGuardian/kachujin_anim_waving.fbx";
        private const string AnimNodPath = "Assets/Characters/ZenGuardian/kachujin_anim_head_nod.fbx";
        private const string AnimTalkPath = "Assets/Characters/ZenGuardian/kachujin_anim_talking.fbx";
        private const string AnimHandshakePath = "Assets/Characters/ZenGuardian/kachujin_anim_handshake.fbx";
        private const string ControllerPath = "Assets/Characters/ZenGuardian/ZenGuardian_Animator.controller";

        [MenuItem("Tools/Zen Garden VR/Setup Zen Guardian in Pavilion", false, 10)]
        public static void SetupZenGuardianMenu()
        {
            Debug.Log("[Zen Guardian] Iniciando configuración del personaje virtual en el pabellón...");

            // 1. Crear / Asegurar Animator Controller
            AnimatorController animController = CreateOrGetAnimatorController();

            // 2. Instanciar o localizar al personaje en la escena
            GameObject guardianGO = GameObject.Find("Zen_Guardian_Kachujin");
            if (guardianGO == null)
            {
                GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbxPath);
                if (fbxPrefab != null)
                {
                    guardianGO = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
                    guardianGO.name = "Zen_Guardian_Kachujin";
                }
                else
                {
                    guardianGO = new GameObject("Zen_Guardian_Kachujin");
                }
            }

            // 4. Mover al guardián más al frente del pabellón para mejor presencia visual e interacción
            guardianGO.transform.position = new Vector3(0f, 0.62f, 17.5f);
            guardianGO.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Mirando hacia el jardín
            guardianGO.transform.localScale = Vector3.one * 1.05f;

            // 5. Configurar Animator
            Animator animator = guardianGO.GetComponent<Animator>();
            if (animator == null) animator = guardianGO.AddComponent<Animator>();
            animator.runtimeAnimatorController = animController;
            animator.applyRootMotion = false;

            // 6. Configurar Colliders y XR Interactable
            CapsuleCollider capsule = guardianGO.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = guardianGO.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            XRSimpleInteractable simpleInteractable = guardianGO.GetComponent<XRSimpleInteractable>();
            if (simpleInteractable == null) simpleInteractable = guardianGO.AddComponent<XRSimpleInteractable>();

            // Asignar materiales URP
            AssignGuardianMaterials(guardianGO);

            // 7. Configurar ZenGuardianController con referencias directas de cabeza y cuello
            ZenGuardianController controller = guardianGO.GetComponent<ZenGuardianController>();
            if (controller == null) controller = guardianGO.AddComponent<ZenGuardianController>();

            // Autoasignar huesos en inspector
            Transform head = FindDeepChild(guardianGO.transform, "mixamorig:Head");
            Transform neck = FindDeepChild(guardianGO.transform, "mixamorig:Neck");
            var soController = new SerializedObject(controller);
            if (head != null) soController.FindProperty("headBone").objectReferenceValue = head;
            if (neck != null) soController.FindProperty("neckBone").objectReferenceValue = neck;
            soController.FindProperty("gazeTrackingSpeed").floatValue = 5.0f;
            soController.FindProperty("bodyRotationSpeed").floatValue = 3.5f;
            soController.FindProperty("proximityDistance").floatValue = 35.0f;
            soController.FindProperty("intimateDistance").floatValue = 4.0f;
            soController.ApplyModifiedProperties();

            // Decorar pabellón y reubicar tatami
            DecoratePavilionAtmosphere(guardianGO.transform.position);

            // 8. Configurar todos los faroles de la escena como interactivos
            SetupAllLanternsInteractive();

            // 9. Crear burbuja de diálogo World Space
            CreateSpeechBubble(guardianGO);

            // 10. Enlazar interactables
            HookSceneInteractables(controller);

            // Marcar escena como modificada
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(guardianGO.scene);
            Debug.Log("[Zen Guardian] ¡Personaje configurado exitosamente en el Pabellón de Té!");
        }

        private static AnimatorController CreateOrGetAnimatorController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

                // Agregar parámetros
                controller.AddParameter("State", AnimatorControllerParameterType.Int);
                controller.AddParameter("TriggerWave", AnimatorControllerParameterType.Trigger);
                controller.AddParameter("TriggerNod", AnimatorControllerParameterType.Trigger);
                controller.AddParameter("TriggerTalk", AnimatorControllerParameterType.Trigger);
                controller.AddParameter("TriggerShakeHands", AnimatorControllerParameterType.Trigger);

                var rootStateMachine = controller.layers[0].stateMachine;

                // Cargar clips de animación desde los FBX
                AnimationClip idleClip = LoadClipFromFbx(AnimIdlePath);
                AnimationClip waveClip = LoadClipFromFbx(AnimWavePath);
                AnimationClip nodClip = LoadClipFromFbx(AnimNodPath);
                AnimationClip talkClip = LoadClipFromFbx(AnimTalkPath);
                AnimationClip handshakeClip = LoadClipFromFbx(AnimHandshakePath);

                var stateIdle = rootStateMachine.AddState("Meditating_Idle");
                stateIdle.motion = idleClip;
                rootStateMachine.defaultState = stateIdle;

                var stateWave = rootStateMachine.AddState("Waving_Greeting");
                stateWave.motion = waveClip;

                var stateNod = rootStateMachine.AddState("HeadNod_Respect");
                stateNod.motion = nodClip;

                var stateTalk = rootStateMachine.AddState("Talking_Wisdom");
                stateTalk.motion = talkClip;

                var stateHandshake = rootStateMachine.AddState("ShakingHands_Friendship");
                stateHandshake.motion = handshakeClip;

                // Transiciones desde AnyState con Triggers
                AddAnyStateTransition(rootStateMachine, stateWave, "TriggerWave");
                AddAnyStateTransition(rootStateMachine, stateNod, "TriggerNod");
                AddAnyStateTransition(rootStateMachine, stateTalk, "TriggerTalk");
                AddAnyStateTransition(rootStateMachine, stateHandshake, "TriggerShakeHands");

                // Transiciones de regreso a Idle al terminar
                AddExitToIdleTransition(stateWave, stateIdle);
                AddExitToIdleTransition(stateNod, stateIdle);
                AddExitToIdleTransition(stateTalk, stateIdle);
                AddExitToIdleTransition(stateHandshake, stateIdle);

                AssetDatabase.SaveAssets();
            }
            return controller;
        }

        private static AnimationClip LoadClipFromFbx(string fbxPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var a in assets)
            {
                if (a is AnimationClip clip && !clip.name.Contains("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }

        private static void AddAnyStateTransition(AnimatorStateMachine sm, AnimatorState targetState, string triggerName)
        {
            var trans = sm.AddAnyStateTransition(targetState);
            trans.AddCondition(AnimatorConditionMode.If, 0, triggerName);
            trans.duration = 0.2f;
            trans.canTransitionToSelf = false;
        }

        private static void AddExitToIdleTransition(AnimatorState fromState, AnimatorState toIdle)
        {
            var trans = fromState.AddTransition(toIdle);
            trans.hasExitTime = true;
            trans.exitTime = 0.88f;
            trans.duration = 0.25f;
        }

        private static void CreateSpeechBubble(GameObject guardianGO)
        {
            Transform existingBubble = guardianGO.transform.Find("SpeechBubble_Canvas");
            if (existingBubble != null) return;

            GameObject canvasGO = new GameObject("SpeechBubble_Canvas");
            canvasGO.transform.SetParent(guardianGO.transform, false);
            canvasGO.transform.localPosition = new Vector3(0f, 2.15f, 0f);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rect = canvasGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300f, 100f);
            rect.localScale = Vector3.one * 0.004f;

            // Fondo translúcido japonés
            GameObject panelGO = new GameObject("Bubble_Panel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            UnityEngine.UI.Image img = panelGO.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.12f, 0.12f, 0.15f, 0.88f);
            RectTransform pRect = panelGO.GetComponent<RectTransform>();
            pRect.anchorMin = Vector2.zero;
            pRect.anchorMax = Vector2.one;
            pRect.sizeDelta = Vector2.zero;

            // Texto descriptivo con TMP
            GameObject textGO = new GameObject("Bubble_Text");
            textGO.transform.SetParent(panelGO.transform, false);
            TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "¡Bienvenido, viajero de Kioto!";
            tmp.fontSize = 22;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.95f, 0.8f, 1f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            RectTransform tRect = textGO.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.sizeDelta = new Vector2(-20, -15);
        }

        private static void HookSceneInteractables(ZenGuardianController controller)
        {
            var so = new SerializedObject(controller);

            GameObject stoneGO = GameObject.Find("Interactive_ZenStone");
            if (stoneGO != null)
            {
                so.FindProperty("zenStoneInteractable").objectReferenceValue = stoneGO.GetComponent<XRBaseInteractable>();
            }

            GameObject lanternGO = GameObject.Find("Interactive_ToroLantern_FlameCore");
            if (lanternGO != null)
            {
                so.FindProperty("lanternInteractable").objectReferenceValue = lanternGO.GetComponent<XRBaseInteractable>();
            }

            GameObject bonsaiGO = GameObject.Find("Interactive_Bonsai_Tree");
            if (bonsaiGO != null)
            {
                so.FindProperty("bonsaiInteractable").objectReferenceValue = bonsaiGO.GetComponent<XRBaseInteractable>();
            }

            Transform speechBubble = controller.transform.Find("SpeechBubble_Canvas");
            if (speechBubble != null)
            {
                so.FindProperty("speechBubbleRoot").objectReferenceValue = speechBubble.gameObject;
                TextMeshProUGUI tmp = speechBubble.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) so.FindProperty("speechText").objectReferenceValue = tmp;
            }

            so.ApplyModifiedProperties();
        }

        private static void AssignGuardianMaterials(GameObject guardianGO)
        {
            Material matBody = AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/ZenGuardian/Materials/M_Kachujin_Body.mat");
            Material matHead = AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/ZenGuardian/Materials/M_Kachujin_Head.mat");

            var renderers = guardianGO.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].name.Contains("kachujin") || mats[i].name.Contains("Default") || mats[i].name.Contains("Material"))
                    {
                        if (i == 0 && matBody != null) mats[i] = matBody;
                        else if (matHead != null) mats[i] = matHead;
                    }
                }
                r.sharedMaterials = mats;
            }
        }

        private static void DecoratePavilionAtmosphere(Vector3 guardianPos)
        {
            // Tatami mat debajo del guardián
            GameObject mat = GameObject.Find("Pavilion_TatamiMat");
            if (mat == null)
            {
                mat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mat.name = "Pavilion_TatamiMat";
                Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ZenGarden/M_ZenWoodDark.mat");
                if (woodMat != null) mat.GetComponent<Renderer>().sharedMaterial = woodMat;
            }
            mat.transform.position = new Vector3(guardianPos.x, 0.61f, guardianPos.z);
            mat.transform.localScale = new Vector3(2.2f, 0.04f, 2.2f);
            mat.transform.rotation = Quaternion.identity;

            // Luz suave cálida del pabellón para resaltar al personaje
            GameObject lightGO = GameObject.Find("Pavilion_GuardianLight");
            if (lightGO == null)
            {
                lightGO = new GameObject("Pavilion_GuardianLight");
                Light light = lightGO.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.92f, 0.78f); // Luz cálida zen
                light.range = 10.0f;
                light.intensity = 3.5f;
            }
            lightGO.transform.position = new Vector3(guardianPos.x, 2.6f, guardianPos.z - 0.5f);
        }

        private static void SetupAllLanternsInteractive()
        {
            string[] lanternCores = new string[] {
                "StoneLantern_Toro_0_FlameCore",
                "Interactive_ToroLantern_FlameCore",
                "StoneLantern_Toro_2_FlameCore",
                "StoneLantern_Toro_3_FlameCore"
            };

            string[] lanternLights = new string[] {
                "StoneLantern_Toro_0_Light",
                "Interactive_ToroLantern_Light",
                "StoneLantern_Toro_2_Light",
                "StoneLantern_Toro_3_Light"
            };

            for (int i = 0; i < lanternCores.Length; i++)
            {
                GameObject coreGO = GameObject.Find(lanternCores[i]);
                GameObject lightGO = GameObject.Find(lanternLights[i]);

                if (coreGO != null)
                {
                    // Asegurar BoxCollider
                    BoxCollider col = coreGO.GetComponent<BoxCollider>();
                    if (col == null) col = coreGO.AddComponent<BoxCollider>();

                    // Asegurar XRSimpleInteractable
                    XRSimpleInteractable si = coreGO.GetComponent<XRSimpleInteractable>();
                    if (si == null) si = coreGO.AddComponent<XRSimpleInteractable>();

                    // Asegurar LanternInteraction
                    ZenGarden.Interactions.LanternInteraction li = coreGO.GetComponent<ZenGarden.Interactions.LanternInteraction>();
                    if (li == null) li = coreGO.AddComponent<ZenGarden.Interactions.LanternInteraction>();

                    // Asignar luz y renderer en SerializedObject
                    var so = new SerializedObject(li);
                    if (lightGO != null)
                    {
                        so.FindProperty("lanternLight").objectReferenceValue = lightGO.GetComponent<Light>();
                    }
                    so.FindProperty("fireFlameRenderer").objectReferenceValue = coreGO.GetComponent<Renderer>();
                    so.ApplyModifiedProperties();
                }
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform result = FindDeepChild(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
