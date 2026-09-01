using System.Collections;
using OopsItAte.Actors;
using UnityEngine;

#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
#endif

namespace OopsItAte.Levels
{
    public sealed class RoomCompletionAnimation : MonoBehaviour
    {
        private const string ResourceName = "OopsItAteCompletionFrames";
        private const string EditorSourcePath = "Assets/Assets/OopsIAte.aseprite";
        private const float SourceCanvasHeightInWorldUnits = 1.08f;

        private PetBody[] pets;
        private RoomCompletionAnimationData animationData;
        private bool hasPlayed;

        public void Initialize(PetBody[] scenePets)
        {
            pets = scenePets;
            animationData = Resources.Load<RoomCompletionAnimationData>(ResourceName);
        }

        private void Update()
        {
            if (hasPlayed || pets == null || pets.Length == 0 || !HaveAllPetsBeenFed())
            {
                return;
            }

            hasPlayed = true;
            StartCoroutine(PlayCompletionAnimation());
        }

        private bool HaveAllPetsBeenFed()
        {
            for (int i = 0; i < pets.Length; i++)
            {
                if (pets[i] == null || !pets[i].HasBeenFed)
                {
                    return false;
                }
            }

            return true;
        }

        private IEnumerator PlayCompletionAnimation()
        {
            Sprite[] frames = GetFrames();
            if (frames == null || frames.Length == 0)
            {
                Debug.LogWarning("Oops It Ate completion animation has no frames.", this);
                yield break;
            }

            Camera camera = Camera.main;
            GameObject visual = new GameObject("Oops It Ate Completion Animation");
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = short.MaxValue;

            if (camera != null)
            {
                visual.transform.SetParent(camera.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0f, camera.nearClipPlane + 1f);
                float visibleWorldHeight = camera.orthographicSize * 2f;
                float scale = visibleWorldHeight * 0.72f / SourceCanvasHeightInWorldUnits;
                visual.transform.localScale = Vector3.one * scale;
            }

            float framesPerSecond = animationData != null
                ? animationData.FramesPerSecond
                : 12f;
            float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
            for (int i = 0; i < frames.Length; i++)
            {
                renderer.sprite = frames[i];
                yield return new WaitForSecondsRealtime(frameDuration);
            }

            float finalFrameHold = animationData != null
                ? animationData.FinalFrameHold
                : 0.45f;
            if (finalFrameHold > 0f)
            {
                yield return new WaitForSecondsRealtime(finalFrameHold);
            }

            Destroy(visual);
        }

        private Sprite[] GetFrames()
        {
            if (animationData != null && animationData.Frames != null
                && animationData.Frames.Length > 0)
            {
                return animationData.Frames;
            }

#if UNITY_EDITOR
            return AssetDatabase.LoadAllAssetsAtPath(EditorSourcePath)
                .OfType<Sprite>()
                .OrderBy(sprite => GetFrameNumber(sprite.name))
                .ThenBy(sprite => sprite.name, StringComparer.Ordinal)
                .ToArray();
#else
            return null;
#endif
        }

#if UNITY_EDITOR
        private static int GetFrameNumber(string spriteName)
        {
            int underscoreIndex = spriteName.LastIndexOf('_');
            return underscoreIndex >= 0
                && int.TryParse(spriteName.Substring(underscoreIndex + 1), out int frameNumber)
                    ? frameNumber
                    : int.MaxValue;
        }
#endif
    }
}
