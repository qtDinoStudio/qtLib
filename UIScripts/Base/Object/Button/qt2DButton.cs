using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace qtLib.UIScripts.Base.Object.Button
{
    [RequireComponent(typeof(Collider2D))]
    public class qt2DButton : MonoBehaviour, IPointerClickHandler
    {
        #region ----- Component Config -----

        public event Action onClick;

        #endregion
        
        public void OnPointerClick(PointerEventData eventData)
        {
            _PlaySfx();
            onClick?.Invoke();
        }
        
        protected virtual void _PlaySfx(){}
        
    }
}