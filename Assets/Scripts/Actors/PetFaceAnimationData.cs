using UnityEngine;

namespace OopsItAte.Actors
{
    public sealed class PetFaceAnimationData : ScriptableObject
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(1f)] private float framesPerSecond = 12f;

        public Sprite[] Frames => frames;
        public float FramesPerSecond => framesPerSecond;

#if UNITY_EDITOR
        public void Configure(Sprite[] animationFrames, float fps)
        {
            frames = animationFrames;
            framesPerSecond = Mathf.Max(1f, fps);
        }
#endif
    }
}
