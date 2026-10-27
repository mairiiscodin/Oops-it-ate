using System;
using UnityEngine;

namespace OopsItAte.Audio
{
    /// <summary>
    /// Placeholder 8-bit sound effects synthesised at runtime. Replace any of them by
    /// dropping a clip named after the <see cref="Sfx"/> value into Resources/Audio/Sfx.
    /// </summary>
    public static class ProceduralSfx
    {
        private const int SampleRate = 44100;

        private enum Wave
        {
            Sine,
            Square,
            Triangle,
            Saw
        }

        // Note frequencies used by the jingles.
        private const float C5 = 523.25f;
        private const float E5 = 659.25f;
        private const float G5 = 783.99f;
        private const float C6 = 1046.5f;
        private const float E6 = 1318.5f;

        public static AudioClip Create(Sfx sfx)
        {
            float[] samples;
            switch (sfx)
            {
                case Sfx.Step: samples = Step(); break;
                case Sfx.Bump: samples = Bump(); break;
                case Sfx.Pickup: samples = Pickup(); break;
                case Sfx.Eat: samples = Eat(); break;
                case Sfx.Grow: samples = Grow(); break;
                case Sfx.Push: samples = Push(); break;
                case Sfx.Burp: samples = Burp(); break;
                case Sfx.BorderPush: samples = BorderPush(); break;
                case Sfx.Reveal: samples = Reveal(); break;
                case Sfx.DoorEnter: samples = DoorEnter(); break;
                case Sfx.Complete: samples = Complete(); break;
                case Sfx.Reset: samples = ResetRoom(); break;
                case Sfx.Nope: samples = Nope(); break;
                default: samples = new float[1]; break;
            }

            SoftClip(samples);
            AudioClip clip = AudioClip.Create($"{sfx} (generated)", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float[] Step()
        {
            float[] buffer = Buffer(0.07f);
            Noise(buffer, 0f, 0.035f, 0.35f, 0.25f, seed: 1);
            Tone(buffer, 0f, 0.06f, 140f, 90f, Wave.Triangle, 0.35f);
            return buffer;
        }

        private static float[] Bump()
        {
            float[] buffer = Buffer(0.1f);
            Tone(buffer, 0f, 0.09f, 110f, 70f, Wave.Square, 0.3f);
            Noise(buffer, 0f, 0.03f, 0.25f, 0.2f, seed: 2);
            return buffer;
        }

        private static float[] Pickup()
        {
            float[] buffer = Buffer(0.18f);
            Tone(buffer, 0f, 0.07f, E5, E5, Wave.Square, 0.25f, duty: 0.25f);
            Tone(buffer, 0.06f, 0.11f, C6, C6, Wave.Square, 0.25f, duty: 0.25f);
            return buffer;
        }

        /// <summary>Three short low-passed noise bursts: "chomp chomp chomp".</summary>
        private static float[] Eat()
        {
            float[] buffer = Buffer(0.3f);
            for (int i = 0; i < 3; i++)
            {
                float start = i * 0.09f;
                Noise(buffer, start, 0.05f, 0.55f, 0.12f, seed: 10 + i);
                Tone(buffer, start, 0.05f, 220f - i * 20f, 120f, Wave.Triangle, 0.3f);
            }

            return buffer;
        }

        /// <summary>Balloon-like rising "bwoop" ending in a small pop.</summary>
        private static float[] Grow()
        {
            float[] buffer = Buffer(0.34f);
            Tone(buffer, 0f, 0.28f, 170f, 520f, Wave.Sine, 0.55f, vibratoHz: 18f, vibratoDepth: 0.04f, attack: 0.02f);
            Tone(buffer, 0f, 0.28f, 170f, 520f, Wave.Square, 0.12f, duty: 0.4f, attack: 0.02f);
            Noise(buffer, 0.27f, 0.05f, 0.45f, 0.6f, seed: 20);
            return buffer;
        }

        private static float[] Push()
        {
            float[] buffer = Buffer(0.18f);
            Noise(buffer, 0f, 0.16f, 0.3f, 0.08f, seed: 30, attack: 0.02f);
            Tone(buffer, 0f, 0.1f, 95f, 60f, Wave.Sine, 0.45f);
            return buffer;
        }

        /// <summary>Wobbly low sawtooth: a comedic burp.</summary>
        private static float[] Burp()
        {
            float[] buffer = Buffer(0.5f);
            Tone(buffer, 0f, 0.46f, 105f, 80f, Wave.Saw, 0.4f, vibratoHz: 26f, vibratoDepth: 0.12f, attack: 0.03f);
            Tone(buffer, 0f, 0.46f, 52f, 40f, Wave.Square, 0.18f, attack: 0.03f);
            Noise(buffer, 0f, 0.4f, 0.12f, 0.05f, seed: 40, attack: 0.03f);
            LowPass(buffer, 0.18f);
            return buffer;
        }

        private static float[] BorderPush()
        {
            float[] buffer = Buffer(0.38f);
            Tone(buffer, 0f, 0.34f, 75f, 45f, Wave.Sine, 0.7f);
            Noise(buffer, 0f, 0.3f, 0.35f, 0.05f, seed: 50);
            return buffer;
        }

        private static float[] Reveal()
        {
            float[] buffer = Buffer(0.5f);
            float[] notes = { C5, E5, G5, C6 };
            for (int i = 0; i < notes.Length; i++)
            {
                float start = i * 0.07f;
                Tone(buffer, start, 0.5f - start, notes[i], notes[i], Wave.Triangle, 0.3f);
                Tone(buffer, start, 0.12f, notes[i] * 2f, notes[i] * 2f, Wave.Sine, 0.08f);
            }

            return buffer;
        }

        private static float[] DoorEnter()
        {
            float[] buffer = Buffer(0.32f);
            Noise(buffer, 0f, 0.3f, 0.35f, 0.02f, seed: 60, attack: 0.12f, cutoffEnd: 0.4f);
            return buffer;
        }

        private static float[] Complete()
        {
            float[] buffer = Buffer(0.95f);
            float[] notes = { C5, E5, G5, C6 };
            for (int i = 0; i < notes.Length; i++)
            {
                Tone(buffer, i * 0.09f, 0.1f, notes[i], notes[i], Wave.Square, 0.2f, duty: 0.25f);
            }

            // Final chord rings out.
            float chordStart = 0.38f;
            float[] chord = { C6, E6, G5 };
            for (int i = 0; i < chord.Length; i++)
            {
                Tone(buffer, chordStart, 0.55f, chord[i], chord[i], Wave.Square, 0.13f, duty: 0.5f,
                    vibratoHz: 6f, vibratoDepth: 0.006f);
            }

            return buffer;
        }

        private static float[] ResetRoom()
        {
            float[] buffer = Buffer(0.28f);
            Tone(buffer, 0f, 0.26f, 620f, 180f, Wave.Square, 0.2f, duty: 0.3f);
            return buffer;
        }

        private static float[] Nope()
        {
            float[] buffer = Buffer(0.2f);
            Tone(buffer, 0f, 0.07f, 220f, 220f, Wave.Square, 0.2f, duty: 0.5f);
            Tone(buffer, 0.09f, 0.09f, 165f, 165f, Wave.Square, 0.2f, duty: 0.5f);
            return buffer;
        }

        private static float[] Buffer(float seconds)
        {
            return new float[Mathf.CeilToInt(seconds * SampleRate)];
        }

        /// <summary>Adds an oscillator with an exponential sweep, quick attack and smooth decay.</summary>
        private static void Tone(
            float[] buffer,
            float start,
            float duration,
            float startFrequency,
            float endFrequency,
            Wave wave,
            float volume,
            float duty = 0.5f,
            float vibratoHz = 0f,
            float vibratoDepth = 0f,
            float attack = 0.005f)
        {
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.RoundToInt(duration * SampleRate);
            double phase = 0d;
            for (int i = 0; i < count && first + i < buffer.Length; i++)
            {
                float t = i / (float)count;
                float frequency = startFrequency * Mathf.Pow(endFrequency / startFrequency, t);
                if (vibratoHz > 0f)
                {
                    frequency *= 1f + Mathf.Sin(2f * Mathf.PI * vibratoHz * i / SampleRate) * vibratoDepth;
                }

                phase += frequency / SampleRate;
                float cycle = (float)(phase - Math.Floor(phase));
                float sample;
                switch (wave)
                {
                    case Wave.Square: sample = cycle < duty ? 1f : -1f; break;
                    case Wave.Triangle: sample = 1f - 4f * Mathf.Abs(cycle - 0.5f); break;
                    case Wave.Saw: sample = 2f * cycle - 1f; break;
                    default: sample = Mathf.Sin(2f * Mathf.PI * cycle); break;
                }

                buffer[first + i] += sample * volume * Envelope(i, count, attack);
            }
        }

        /// <summary>Adds white noise through a one-pole low-pass (cutoff 0..1, higher = brighter).</summary>
        private static void Noise(
            float[] buffer,
            float start,
            float duration,
            float volume,
            float cutoff,
            int seed,
            float attack = 0.002f,
            float cutoffEnd = -1f)
        {
            var random = new System.Random(seed);
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.RoundToInt(duration * SampleRate);
            float filtered = 0f;
            for (int i = 0; i < count && first + i < buffer.Length; i++)
            {
                float t = i / (float)count;
                float alpha = cutoffEnd >= 0f ? Mathf.Lerp(cutoff, cutoffEnd, t) : cutoff;
                float white = (float)(random.NextDouble() * 2d - 1d);
                filtered += (white - filtered) * Mathf.Clamp01(alpha);
                buffer[first + i] += filtered * volume * Envelope(i, count, attack);
            }
        }

        private static float Envelope(int index, int count, float attackSeconds)
        {
            int attackSamples = Mathf.Max(1, Mathf.RoundToInt(attackSeconds * SampleRate));
            float attack = Mathf.Clamp01(index / (float)attackSamples);
            float remaining = 1f - index / (float)count;
            return attack * remaining * remaining;
        }

        private static void LowPass(float[] buffer, float alpha)
        {
            float filtered = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                filtered += (buffer[i] - filtered) * alpha;
                buffer[i] = filtered;
            }
        }

        /// <summary>Gentle saturation so stacked layers never clip harshly.</summary>
        private static void SoftClip(float[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                float x = buffer[i];
                buffer[i] = x / (1f + Mathf.Abs(x));
            }
        }
    }
}
