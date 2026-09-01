using UnityEngine;

namespace OopsItAte.Levels
{
    public sealed class RoomCompletionAnimationData : ScriptableObject
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(1f)] private float framesPerSecond = 12f;
        [SerializeField, Min(0f)] private float finalFrameHold = 0.45f;

        public Sprite[] Frames => frames;
        public float FramesPerSecond => framesPerSecond;
        public float FinalFrameHold => finalFrameHold;
    }
}
