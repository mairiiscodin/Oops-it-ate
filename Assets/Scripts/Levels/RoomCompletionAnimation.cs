using System.Collections;
using System.Collections.Generic;
using OopsItAte.Actors;
using OopsItAte.Grid;
using OopsItAte.Input;
using OopsItAte.Interaction;
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
        private string roomId;
        private bool hasPlayed;
        private KeyboardGridInput gameplayInput;
        private GridMover playerMover;
        private GameObject animationVisual;
        private float timeScaleBeforeAnimation = 1f;
        private bool pausedGameplay;
        private bool gameplayInputWasEnabled;

        public void Initialize(PetBody[] scenePets, string currentRoomId)
        {
            var actualPets = new List<PetBody>();
            if (scenePets != null)
            {
                for (int i = 0; i < scenePets.Length; i++)
                {
                    PetBody pet = scenePets[i];
                    if (pet != null && pet.GetComponent<KitchenStation>() == null)
                    {
                        actualPets.Add(pet);
                    }
                }
            }

            pets = actualPets.ToArray();
            roomId = currentRoomId;
            hasPlayed = GameSession.HasRoomCompletionPlayed(roomId);
            animationData = Resources.Load<RoomCompletionAnimationData>(ResourceName);
            gameplayInput = FindAnyObjectByType<KeyboardGridInput>();
            playerMover = FindAnyObjectByType<GridMover>();
        }

        private void OnDisable()
        {
            ResumeGameplay();
            if (animationVisual != null)
            {
                Destroy(animationVisual);
                animationVisual = null;
            }
        }

        private void Update()
        {
            if (hasPlayed
                || pets == null
                || pets.Length == 0
                || !HaveAllPetsBeenFed()
                || (playerMover != null && playerMover.IsMoving))
            {
                return;
            }

            hasPlayed = true;
            GameSession.MarkRoomCompletionPlayed(roomId);
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

            PauseGameplay();
            Camera camera = Camera.main;
            animationVisual = new GameObject("Oops It Ate Completion Animation");
            SpriteRenderer renderer = animationVisual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = short.MaxValue;

            if (camera != null)
            {
                animationVisual.transform.SetParent(camera.transform, false);
                float visibleWorldHeight = camera.orthographicSize * 2f;
                float scale = visibleWorldHeight * 0.72f / SourceCanvasHeightInWorldUnits;
                animationVisual.transform.localScale = Vector3.one * scale;

                // Aseprite trims transparent pixels while preserving the original
                // canvas through an offset pivot. Offset the whole sequence by the
                // final logo's visual center so the authored motion stays intact
                // and the completed "Oops I Ate" image lands at screen center.
                Vector3 logoCenter = frames[frames.Length - 1].bounds.center * scale;
                animationVisual.transform.localPosition = new Vector3(
                    -logoCenter.x,
                    -logoCenter.y,
                    camera.nearClipPlane + 1f);
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

            Destroy(animationVisual);
            animationVisual = null;
            ResumeGameplay();
        }

        private void PauseGameplay()
        {
            if (pausedGameplay)
            {
                return;
            }

            pausedGameplay = true;
            timeScaleBeforeAnimation = Time.timeScale;
            Time.timeScale = 0f;
            if (gameplayInput != null)
            {
                gameplayInputWasEnabled = gameplayInput.enabled;
                gameplayInput.enabled = false;
            }
        }

        private void ResumeGameplay()
        {
            if (!pausedGameplay)
            {
                return;
            }

            Time.timeScale = timeScaleBeforeAnimation;
            if (gameplayInput != null)
            {
                gameplayInput.enabled = gameplayInputWasEnabled;
            }

            pausedGameplay = false;
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
