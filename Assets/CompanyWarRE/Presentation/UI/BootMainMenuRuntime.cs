using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyWarRE.Presentation.UI
{
    /// <summary>Updates the authored clock in the Boot main menu.</summary>
    public sealed class BootMainMenuClock : MonoBehaviour
    {
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private TMP_Text dateText;
        private int _renderedMinute = -1;

        public void Configure(TMP_Text clock, TMP_Text date)
        {
            clockText = clock;
            dateText = date;
        }

        private void OnEnable()
        {
            _renderedMinute = -1;
            Refresh();
        }

        private void Update()
        {
            if (DateTime.Now.Minute != _renderedMinute)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            var now = DateTime.Now;
            _renderedMinute = now.Minute;
            if (clockText != null) clockText.text = now.ToString("HH:mm");
            if (dateText != null)
            {
                string[] weekdays =
                {
                    "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"
                };
                dateText.text = $"{now:yyyy/MM/dd}  {weekdays[(int)now.DayOfWeek]}";
            }
        }
    }

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

    /// <summary>Draws the five-point corporate wing silhouette used by the menu buttons.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BootMenuWingGraphic : MaskableGraphic
    {
        [SerializeField] private bool pointRight = true;
        [SerializeField, Range(0.02f, 0.35f)] private float pointRatio = 0.16f;
        [SerializeField, Range(0f, 0.25f)] private float cutRatio = 0.08f;
        [SerializeField, Range(0.05f, 0.45f)] private float tipTopRatio = 0.16f;

        public void Configure(bool pointsRight, float point = 0.16f, float cut = 0.08f)
        {
            pointRight = pointsRight;
            pointRatio = Mathf.Clamp(point, 0.02f, 0.35f);
            cutRatio = Mathf.Clamp(cut, 0f, 0.25f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            var tip = rect.width * pointRatio;
            var cut = rect.width * cutRatio;
            var tipY = rect.yMax - rect.height * tipTopRatio;
            Vector2[] points = pointRight
                ? new[]
                {
                    new Vector2(rect.xMin + cut, rect.yMax),
                    new Vector2(rect.xMax - tip * 0.18f, rect.yMax),
                    new Vector2(rect.xMax, tipY),
                    new Vector2(rect.xMax - tip, rect.yMin),
                    new Vector2(rect.xMin, rect.yMin)
                }
                : new[]
                {
                    new Vector2(rect.xMax - cut, rect.yMax),
                    new Vector2(rect.xMin + tip * 0.18f, rect.yMax),
                    new Vector2(rect.xMin, tipY),
                    new Vector2(rect.xMin + tip, rect.yMin),
                    new Vector2(rect.xMax, rect.yMin)
                };

            var center = Vector2.zero;
            foreach (var point in points) center += point;
            center /= points.Length;

            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = center;
            vertexHelper.AddVert(vertex);
            foreach (var point in points)
            {
                vertex.position = point;
                vertexHelper.AddVert(vertex);
            }

            for (var index = 1; index <= points.Length; index++)
            {
                vertexHelper.AddTriangle(0, index, index == points.Length ? 1 : index + 1);
            }
        }
    }

}
