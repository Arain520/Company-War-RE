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
}
