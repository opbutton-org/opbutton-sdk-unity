using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OpButton.SDK
{
    /// <summary>A pointer command owns its press until release or cancellation.</summary>
    internal sealed class OpButtonPointerGesture
    {
        private readonly PointerEventData m_data;
        private readonly GameObject m_target;
        internal bool IsActive { get; private set; } = true;

        private OpButtonPointerGesture(PointerEventData data, GameObject target)
        {
            m_data = data;
            m_target = target;
        }

        internal static OpButtonPointerGesture Begin(EventSystem eventSystem, Vector2 position)
        {
            if (eventSystem == null) return null;
            var data = new PointerEventData(eventSystem)
            {
                position = position,
                pressPosition = position,
                button = PointerEventData.InputButton.Left,
                eligibleForClick = true,
                clickCount = 1,
                clickTime = Time.unscaledTime
            };
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(data, hits);
            if (hits.Count == 0) return null;
            var hit = hits[0];
            data.pointerCurrentRaycast = hit;
            data.pointerPressRaycast = hit;
            data.rawPointerPress = hit.gameObject;
            // Text and images can be raycast targets while their parent handles input.
            var target = ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, ExecuteEvents.pointerDownHandler)
                ?? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            if (target == null) return null;
            data.pointerPress = target;
            return new OpButtonPointerGesture(data, target);
        }

        internal void Release(bool cancelled = false)
        {
            if (!IsActive) return;
            IsActive = false;
            if (cancelled) m_data.eligibleForClick = false;
            if (m_target != null)
            {
                ExecuteEvents.Execute(m_target, m_data, ExecuteEvents.pointerUpHandler);
                if (m_data.eligibleForClick) ExecuteEvents.Execute(m_target, m_data, ExecuteEvents.pointerClickHandler);
            }
            m_data.pointerPress = null;
            m_data.rawPointerPress = null;
        }
    }
}
