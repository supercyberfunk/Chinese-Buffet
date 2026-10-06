using UnityEngine;

namespace BuffetSim.UI
{
    /// <summary>Keeps a world-space label facing the main camera.</summary>
    public sealed class Billboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 away = transform.position - cam.transform.position;
            if (away.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
    }
}
