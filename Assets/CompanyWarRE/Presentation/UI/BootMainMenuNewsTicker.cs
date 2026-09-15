using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyWarRE.Presentation.UI
{
    /// <summary>
    /// Compatibility implementation of COW's HudNewsTicker behaviour. The text moves inside a
    /// RectMask2D viewport and wraps after its complete preferred width has left the viewport.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(TextMeshProUGUI))]
    public sealed class BootMainMenuNewsTicker : MonoBehaviour
    {
        [SerializeField] private float scrollSpeed = 90f;
        [SerializeField] private float resetGap = 160f;

        private RectTransform _rectTransform;
        private TextMeshProUGUI _text;
        private RectTransform _viewport;
        private float _startX;

        public void Configure(float speed, float gap)
        {
            scrollSpeed = Mathf.Max(1f, speed);
            resetGap = Mathf.Max(0f, gap);
        }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _text = GetComponent<TextMeshProUGUI>();
            _viewport = transform.parent as RectTransform;
            _startX = _rectTransform.anchoredPosition.x;
        }

        private void OnEnable()
        {
            if (_rectTransform == null) Awake();
            if (_rectTransform != null)
            {
                var position = _rectTransform.anchoredPosition;
                position.x = _startX;
                _rectTransform.anchoredPosition = position;
            }
        }

        private void Update()
        {
            if (_rectTransform == null || _text == null || string.IsNullOrWhiteSpace(_text.text))
            {
                return;
            }

            var position = _rectTransform.anchoredPosition;
            position.x -= scrollSpeed * Time.unscaledDeltaTime;
            var textWidth = Mathf.Max(_text.preferredWidth, _rectTransform.rect.width);
            if (position.x + textWidth < -resetGap)
            {
                position.x = (_viewport != null ? _viewport.rect.width : _startX) + resetGap;
            }

            _rectTransform.anchoredPosition = position;
        }
    }
}
