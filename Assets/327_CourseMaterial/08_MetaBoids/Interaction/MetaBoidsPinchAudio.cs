using System;
using UnityEngine;

/*
 * HANDS -> FM VOICES -> FILTERS
 * Reuse BoidFMSynth from the course: one voice per hand, not per boid.
 * Height changes pitch; movement opens the filter and adds FM brightness.
 */
[RequireComponent(typeof(AudioSource))]
[DefaultExecutionOrder(100)]
public class MetaBoidsPinchAudio : MonoBehaviour
{
    public MetaBoidsPinchInput hands;
    [Range(0f, 0.4f)] public float masterGain = 0.18f;
    [Range(0.01f, 0.2f)] public float smoothingSeconds = 0.045f;
    public Vector2 pitchRange = new Vector2(85f, 220f);
    public Vector2 cutoffRange = new Vector2(350f, 2600f);
    [Range(1f, 8f)] public float resonance = 3f;

    BoidFMSynth[] voices;
    Vector2[] previous = new Vector2[2];
    bool[] wasPinched = new bool[2];
    AudioSource source;
    AudioLowPassFilter lowPass;
    AudioClip clock;
    int sampleRate;
    volatile bool ready;

    /*
     * 1. BUILD THE AUDIO CHAIN
     * A silent looping clip keeps the audio callback running.
     * Filters are added AFTER this generator so they process its output.
     */
    void Start()
    {
        sampleRate = AudioSettings.outputSampleRate;
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        clock = AudioClip.Create("FM clock", sampleRate, 2, sampleRate, false);
        source.clip = clock;
        voices = new[] { gameObject.AddComponent<BoidFMSynth>(), gameObject.AddComponent<BoidFMSynth>() };
        foreach (BoidFMSynth voice in voices) voice.followMotion = false;
        voices[1].SetPhase(1.3f);

        var highPass = gameObject.AddComponent<AudioHighPassFilter>();
        highPass.cutoffFrequency = 45f;
        lowPass = gameObject.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = cutoffRange.x;
        lowPass.lowpassResonanceQ = resonance;
        var echo = gameObject.AddComponent<AudioEchoFilter>();
        echo.delay = 95f;
        echo.decayRatio = 0.25f;
        echo.dryMix = 0.9f;
        echo.wetMix = 0.12f;
        ready = true;
        source.Play();
    }

    /*
     * 2. SEND MUSICAL PARAMETERS FROM THE MAIN THREAD
     * Pinch opens the voice; release fades it out through sample smoothing.
     * Left/right panning follows the hand's screen position.
     */
    void Update()
    {
        float motion = 0f;
        for (int hand = 0; hand < 2; hand++)
        {
            Vector4 state = hand == 0 ? hands.LeftHand : hands.RightHand;
            bool pinched = hands.isActiveAndEnabled && state.z > 0f;
            Vector2 position = new Vector2(state.x, state.y);
            float speed = pinched && wasPinched[hand]
                ? Vector2.Distance(position, previous[hand]) / Mathf.Max(Time.deltaTime, 0.0001f) : 0f;
            speed = Mathf.Clamp01(speed * 2f);
            BoidFMSynth voice = voices[hand];
            voice.carrierFrequency = Mathf.Lerp(pitchRange.x, pitchRange.y, state.y) * (hand == 0 ? 1f : 1.12f);
            voice.modulatorFrequency = voice.carrierFrequency * 1.5f;
            voice.modulationIndex = 1.5f + speed * 7f;
            voice.amplitude = pinched ? 1f : 0f;
            voice.pan = Mathf.Clamp(state.x * 2f - 1f, -0.9f, 0.9f);
            voice.UpdateParameters();
            previous[hand] = position;
            wasPinched[hand] = pinched;
            motion = Mathf.Max(motion, pinched ? 0.2f + speed * 0.8f : 0f);
        }
        lowPass.cutoffFrequency = Mathf.Lerp(lowPass.cutoffFrequency,
            Mathf.Lerp(cutoffRange.x, cutoffRange.y, motion), 1f - Mathf.Exp(-Time.deltaTime * 10f));
        lowPass.lowpassResonanceQ = resonance;
    }

    /*
     * 3. GENERATE SAMPLES ON THE AUDIO THREAD
     * Only oscillator math and copied parameters belong here. No scene API calls.
     * BoidFMSynth smooths pitch, gain, and pan to avoid clicks.
     */
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!ready) { Array.Clear(data, 0, data.Length); return; }
        float smoothing = 1f - (float)Math.Exp(-1f / (sampleRate * smoothingSeconds));
        voices[0].ReadParameters();
        voices[1].ReadParameters();
        for (int i = 0; i < data.Length; i += channels)
        {
            voices[0].Render(sampleRate, smoothing, out float left0, out float right0);
            voices[1].Render(sampleRate, smoothing, out float left1, out float right1);
            float left = (float)Math.Tanh((left0 + left1) * masterGain);
            float right = (float)Math.Tanh((right0 + right1) * masterGain);
            for (int channel = 0; channel < channels; channel++)
                data[i + channel] = channels == 1 ? (left + right) * 0.5f : channel % 2 == 0 ? left : right;
        }
    }

    void OnEnable()
    {
        if (source != null) { ready = true; source.Play(); }
    }

    void OnDisable()
    {
        ready = false;
        if (source != null) source.Stop();
    }

    void OnDestroy() => Destroy(clock);
}
