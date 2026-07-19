#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using OopsItAte.Actors;

namespace OopsItAte.Editor
{
    [InitializeOnLoad]
    public static class PetFatAnimationSetup
    {
        private const string SourcePath = "Assets/Assets/FatDog.aseprite";
        private const string ClipPath = "Assets/Animation/Pet/FatDog_Idle.anim";
        private const string HungrySourcePath = "Assets/Assets/HungryDog.aseprite";
        private const string HungryClipPath = "Assets/Animation/Pet/HungryDog.anim";
        private const string BigDogFaceSourcePath = "Assets/Assets/BigDogFace.aseprite";
        private const string BigDogFaceClipPath = "Assets/Resources/Pet/BigDogFace.anim";
        private const string BigDogFaceDataPath = "Assets/Resources/Pet/BigDogFaceFrames.asset";
        private const string ControllerPath = "Assets/Animation/Pet/PetController.controller";
        private const string FatParameterName = "IsFat";
        private const string HungryParameterName = "IsHungry";

        static PetFatAnimationSetup()
        {
            EditorApplication.delayCall += SetupIfNeeded;
        }

        [MenuItem("Tools/Oops It Ate/Setup Fat Dog Animation")]
        public static void Setup()
        {
            AnimationClip fatClip = CreateOrUpdateClip(
                SourcePath,
                ClipPath,
                "FatDog_Idle",
                "FatDog");
            AnimationClip hungryClip = CreateOrUpdateClip(
                HungrySourcePath,
                HungryClipPath,
                "HungryDog",
                "HungryDog");
            AnimationClip bigDogFaceClip = CreateOrUpdateClip(
                BigDogFaceSourcePath,
                BigDogFaceClipPath,
                "BigDogFace",
                "BigDogFace",
                false,
                true);
            PetFaceAnimationData bigDogFaceData = CreateOrUpdateBigDogFaceData();
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (fatClip == null
                || hungryClip == null
                || bigDogFaceClip == null
                || bigDogFaceData == null
                || controller == null)
            {
                Debug.LogWarning("Pet animation setup could not find FatDog, HungryDog, or PetController.");
                return;
            }

            EnsureControllerStates(controller, fatClip, hungryClip);
            AssetDatabase.SaveAssets();
            Debug.Log("Pet animation setup is ready: DogIdle -> HungryDog -> FatDog.");
        }

        private static void SetupIfNeeded()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            bool hasFatClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath) != null;
            bool hasHungryClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(HungryClipPath) != null;
            bool hasBigDogFaceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BigDogFaceClipPath) != null;
            bool hasBigDogFaceData = AssetDatabase.LoadAssetAtPath<PetFaceAnimationData>(BigDogFaceDataPath) != null;
            bool hasFatParameter = HasParameter(controller, FatParameterName);
            bool hasHungryParameter = HasParameter(controller, HungryParameterName);
            bool hasFatState = HasState(controller, "FatDog");
            bool hasHungryState = HasState(controller, "HungryDog");

            if (!hasFatClip
                || !hasHungryClip
                || !hasBigDogFaceClip
                || !hasBigDogFaceData
                || !hasFatParameter
                || !hasHungryParameter
                || !hasFatState
                || !hasHungryState)
            {
                Setup();
            }
        }

        private static AnimationClip CreateOrUpdateClip(
            string sourcePath,
            string clipPath,
            string clipName,
            string firstFrameName,
            bool loop = true,
            bool legacy = false)
        {
            List<Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                .OfType<Sprite>()
                .OrderBy(sprite => GetFrameIndex(sprite, firstFrameName))
                .ToList();
            if (sprites.Count == 0)
            {
                return null;
            }

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                EnsureAssetFolder(clipPath);
                clip = new AnimationClip
                {
                    name = clipName,
                    frameRate = 12f,
                    legacy = legacy
                };
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            else
            {
                clip.legacy = legacy;
            }

            var keyframes = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i / clip.frameRate,
                    value = sprites[i]
                };
            }

            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(
                string.Empty,
                typeof(SpriteRenderer),
                "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static PetFaceAnimationData CreateOrUpdateBigDogFaceData()
        {
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(BigDogFaceSourcePath)
                .OfType<Sprite>()
                .OrderBy(sprite => GetFrameIndex(sprite, "BigDogFace"))
                .ToArray();
            if (frames.Length == 0)
            {
                return null;
            }

            PetFaceAnimationData data = AssetDatabase.LoadAssetAtPath<PetFaceAnimationData>(
                BigDogFaceDataPath);
            if (data == null)
            {
                EnsureAssetFolder(BigDogFaceDataPath);
                data = ScriptableObject.CreateInstance<PetFaceAnimationData>();
                data.name = "BigDogFaceFrames";
                AssetDatabase.CreateAsset(data, BigDogFaceDataPath);
            }

            data.Configure(frames, 12f);
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void EnsureControllerStates(
            AnimatorController controller,
            AnimationClip fatClip,
            AnimationClip hungryClip)
        {
            EnsureParameter(controller, FatParameterName);
            EnsureParameter(controller, HungryParameterName);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = stateMachine.defaultState;
            AnimatorState fatState = GetOrCreateState(stateMachine, "FatDog", 620f);
            AnimatorState hungryState = GetOrCreateState(stateMachine, "HungryDog", 500f, 230f);

            fatState.motion = fatClip;
            hungryState.motion = hungryClip;

            EnsureTransition(idleState, fatState, FatParameterName, AnimatorConditionMode.If);
            EnsureTransition(fatState, idleState, FatParameterName, AnimatorConditionMode.IfNot);
            EnsureTransition(idleState, hungryState, HungryParameterName, AnimatorConditionMode.If);
            EnsureTransition(
                hungryState,
                idleState,
                HungryParameterName,
                AnimatorConditionMode.IfNot,
                FatParameterName,
                AnimatorConditionMode.IfNot);
            EnsureTransition(
                hungryState,
                fatState,
                HungryParameterName,
                AnimatorConditionMode.IfNot,
                FatParameterName,
                AnimatorConditionMode.If);
            EditorUtility.SetDirty(controller);
        }

        private static void EnsureParameter(AnimatorController controller, string parameterName)
        {
            if (!HasParameter(controller, parameterName))
            {
                controller.AddParameter(parameterName, AnimatorControllerParameterType.Bool);
            }
        }

        private static AnimatorState GetOrCreateState(
            AnimatorStateMachine stateMachine,
            string stateName,
            float x,
            float y = 110f)
        {
            AnimatorState state = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(candidate => candidate.name == stateName);
            if (state == null)
            {
                state = stateMachine.AddState(stateName, new Vector3(x, y, 0f));
            }

            return state;
        }

        private static void EnsureTransition(
            AnimatorState source,
            AnimatorState destination,
            string parameterName,
            AnimatorConditionMode conditionMode)
        {
            if (source.transitions.Any(transition => transition.destinationState == destination))
            {
                return;
            }

            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.AddCondition(conditionMode, 0f, parameterName);
        }

        private static void EnsureTransition(
            AnimatorState source,
            AnimatorState destination,
            string firstParameter,
            AnimatorConditionMode firstMode,
            string secondParameter,
            AnimatorConditionMode secondMode)
        {
            if (source.transitions.Any(transition => transition.destinationState == destination))
            {
                return;
            }

            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.AddCondition(firstMode, 0f, firstParameter);
            transition.AddCondition(secondMode, 0f, secondParameter);
        }

        private static bool HasParameter(AnimatorController controller, string parameterName)
        {
            return controller != null
                && controller.parameters.Any(parameter => parameter.name == parameterName);
        }

        private static bool HasState(AnimatorController controller, string stateName)
        {
            return controller != null
                && controller.layers[0].stateMachine.states.Any(child => child.state.name == stateName);
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string folderPath = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static int GetFrameIndex(Sprite sprite, string firstFrameName)
        {
            if (string.Equals(sprite.name, firstFrameName, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            int separator = sprite.name.LastIndexOf('_');
            return separator >= 0 && int.TryParse(sprite.name.Substring(separator + 1), out int index)
                ? index
                : int.MaxValue;
        }
    }
}
#endif
