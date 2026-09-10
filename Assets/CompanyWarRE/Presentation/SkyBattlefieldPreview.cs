using UnityEngine;

namespace CompanyWarRE.Presentation
{
    /// <summary>Editor preview roots must never duplicate the actual runtime battlefield.</summary>
    public sealed class SkyBattlefieldPreview : MonoBehaviour
    {
        private void Awake()
        {
            if (UnityEngine.Application.isPlaying) gameObject.SetActive(false);
        }
    }
}
