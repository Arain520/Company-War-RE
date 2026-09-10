using UnityEngine;
using UnityEngine.EventSystems;

namespace CompanyWarRE.Presentation.UI
{
    public sealed class FormalBattleDeploymentDragItem : MonoBehaviour,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private FormalBattleHudController _owner;
        private string _unitId;

        public void Configure(FormalBattleHudController owner, string unitId)
        {
            _owner = owner;
            _unitId = unitId;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _owner?.SelectUnit(_unitId);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _owner?.BeginUnitDrag(_unitId, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _owner?.ContinueUnitDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _owner?.EndUnitDrag(eventData.position);
        }
    }
}
