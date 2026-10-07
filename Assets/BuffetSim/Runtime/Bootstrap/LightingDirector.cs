using BuffetSim.Core;
using UnityEngine;

namespace BuffetSim.Bootstrap
{
    /// <summary>
    /// Presentation only: the room's light follows the day (lights off while the summary is up) and
    /// answers tint cues from the bus (the slot machine's red 8-8-8). Sits on the sun; never calls anything.
    /// </summary>
    public sealed class LightingDirector : MonoBehaviour
    {
        private const float ClosedIntensity = 0.12f;
        private const float FadeSpeed = 2.5f;

        private Light _light;
        private Color _baseColor;
        private float _baseIntensity;
        private Color _baseAmbient;
        private Color _cueColor;
        private float _cueUntil = float.NegativeInfinity;
        private bool _closed;

        private void Awake()
        {
            _light = GetComponent<Light>();
            if (_light != null)
            {
                _baseColor = _light.color;
                _baseIntensity = _light.intensity;
            }
            _baseAmbient = RenderSettings.ambientLight;
        }

        private void OnEnable()
        {
            GameEvents.DayPhaseChanged += OnDayPhaseChanged;
            GameEvents.LightingCueRequested += OnLightingCue;
        }

        private void OnDisable()
        {
            GameEvents.DayPhaseChanged -= OnDayPhaseChanged;
            GameEvents.LightingCueRequested -= OnLightingCue;
        }

        private void Update()
        {
            if (_light == null) return;
            bool cue = Time.time < _cueUntil;
            Color targetColor = cue ? _cueColor : _baseColor;
            float targetIntensity = _closed ? ClosedIntensity : _baseIntensity;
            Color targetAmbient = _closed ? _baseAmbient * 0.25f : (cue ? Color.Lerp(_baseAmbient, _cueColor, 0.5f) : _baseAmbient);

            float t = Mathf.Clamp01(FadeSpeed * Time.deltaTime);
            _light.color = Color.Lerp(_light.color, targetColor, t);
            _light.intensity = Mathf.Lerp(_light.intensity, targetIntensity, t);
            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, targetAmbient, t);
        }

        private void OnDayPhaseChanged(DayClockSnapshot snapshot)
        {
            _closed = snapshot.Phase == DayPhase.Closed;
        }

        private void OnLightingCue(Color color, float seconds)
        {
            _cueColor = color;
            _cueUntil = Time.time + Mathf.Max(0f, seconds);
        }
    }
}
