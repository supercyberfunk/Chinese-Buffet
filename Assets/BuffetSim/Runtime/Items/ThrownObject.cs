using System;
using BuffetSim.Bootstrap;
using BuffetSim.Interaction;
using UnityEngine;

namespace BuffetSim.Items
{
    /// <summary>
    /// The flight of something thrown: a primitive that arcs from the hand to a point, or chases a
    /// homing target, and reports where it landed. Presentation plus a callback; the thrower decides
    /// what the hit does.
    /// </summary>
    public sealed class ThrownObject : MonoBehaviour
    {
        private Vector3 _from;
        private Vector3 _to;
        private IThrowTarget _target;
        private float _duration;
        private float _arc;
        private float _age;
        private bool _done;
        private Action<ThrownObject, IThrowTarget, Vector3> _onArrive;

        /// <summary>
        /// Throws a primitive of <paramref name="shape"/> from <paramref name="from"/>. With a
        /// <paramref name="homingTarget"/> it bends towards the target's aim point all the way; without
        /// one it lands at <paramref name="to"/>. <paramref name="onArrive"/> gets the target (null when
        /// there was none or it vanished mid-flight) and the landing point.
        /// </summary>
        public static ThrownObject Launch(PrimitiveType shape, float size, Color color, Vector3 from, Vector3 to, IThrowTarget homingTarget,
            float speed, float arc, Action<ThrownObject, IThrowTarget, Vector3> onArrive)
        {
            GameObject go = PrimitiveFactory.Visual("Thrown", shape, null, from, Vector3.one * size, MaterialLibrary.Get(color));
            ThrownObject thrown = go.AddComponent<ThrownObject>();
            thrown._from = from;
            thrown._to = homingTarget != null ? homingTarget.AimPoint : to;
            thrown._target = homingTarget;
            float distance = Vector3.Distance(from, thrown._to);
            thrown._duration = Mathf.Clamp(distance / Mathf.Max(1f, speed), 0.15f, 2f);
            thrown._arc = arc;
            thrown._onArrive = onArrive;
            return thrown;
        }

        private void Update()
        {
            if (_done) return;
            _age += Time.deltaTime;

            if (_target != null)
            {
                if (_target is UnityEngine.Object unityObject && unityObject == null) _target = null;
                else _to = _target.AimPoint;
            }

            float t = Mathf.Clamp01(_age / _duration);
            Vector3 flat = Vector3.Lerp(_from, _to, t);
            transform.position = flat + Vector3.up * (Mathf.Sin(t * Mathf.PI) * _arc);
            transform.Rotate(540f * Time.deltaTime, 360f * Time.deltaTime, 0f, Space.Self);
            if (t < 1f) return;

            _done = true;
            Action<ThrownObject, IThrowTarget, Vector3> callback = _onArrive;
            _onArrive = null;
            callback?.Invoke(this, _target, _to);
            Destroy(gameObject);
        }
    }
}
