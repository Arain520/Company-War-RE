using TMPro;
using UnityEngine;

namespace YFan.Systems.FloatingText
{
    public sealed class FloatingTextItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private Canvas _canvas;

        private float _remaining;
        private float _duration;

        public void Show(string message, Color color, float duration = 0.8f)
        {
            if (_text == null) _text = GetComponent<TextMeshProUGUI>();
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            if (_text != null)
            {
                _text.text = message ?? string.Empty;
                _text.color = color;
                _text.alpha = 1f;
            }

            _duration = Mathf.Max(0.05f, duration);
            _remaining = _duration;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_remaining <= 0f) return;
            _remaining -= Time.unscaledDeltaTime;
            transform.localPosition += Vector3.up * (40f * Time.unscaledDeltaTime);
            if (_text != null) _text.alpha = Mathf.Clamp01(_remaining / _duration);
            if (_remaining <= 0f) gameObject.SetActive(false);
        }
    }
}
