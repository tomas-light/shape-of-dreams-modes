using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChaosSlots
{
    internal sealed class ChaosSlotHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        private static ChaosSlotHandle _dragging;

        private UI_InGame_GemSlot _slot;
        private Image _hitbox;
        private Image _icon;
        private RectTransform _dragPreview;
        private Canvas _canvas;
        private float _desiredWidth;
        private uint _originalGemNetId;

        internal static ChaosSlotHandle Create(UI_InGame_GemSlot slot)
        {
            var obj = new GameObject("Chaos Slot Handle", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(slot.transform, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0f);
            var slotRect = ((RectTransform)slot.transform).rect;
            float scale = Mathf.Max(24f, Mathf.Min(slotRect.width, slotRect.height));
            rect.sizeDelta = new Vector2(scale, scale * 1.12f);
            rect.anchoredPosition = new Vector2(0f, Mathf.Max(2f, scale * 0.10f));

            var gfx = new GameObject("Chaos Petal", typeof(RectTransform), typeof(Image));
            var gfxRect = (RectTransform)gfx.transform;
            gfxRect.SetParent(rect, false);
            gfxRect.anchorMin = Vector2.zero;
            gfxRect.anchorMax = Vector2.one;
            gfxRect.offsetMin = new Vector2(2f, 1f);
            gfxRect.offsetMax = new Vector2(-2f, -1f);

            var handle = obj.AddComponent<ChaosSlotHandle>();
            handle._slot = slot;
            handle._desiredWidth = rect.sizeDelta.x;
            handle._hitbox = obj.GetComponent<Image>();
            handle._hitbox.color = Color.clear;
            handle._hitbox.canvasRenderer.cullTransparentMesh = false;
            handle._hitbox.raycastTarget = false;
            handle._icon = gfx.GetComponent<Image>();
            handle._icon.sprite = ChaosPetal.Sprite;
            handle._icon.preserveAspect = true;
            handle._icon.raycastTarget = false;
            handle._icon.enabled = false;
            return handle;
        }

        internal void SetMaximumWidth(float width)
        {
            var rect = _hitbox.rectTransform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(_desiredWidth, width));
        }

        internal static bool TryGetDragOrigin(out GemLocation origin)
        {
            origin = default;
            if (_dragging == null || _dragging._dragPreview == null || !_dragging.isActiveAndEnabled)
                return false;
            origin = _dragging._slot.thisSlotLocation;
            var player = DewPlayer.local;
            return ChaosSlotState.CanGrab(player, origin)
                && ChaosSlotState.GetGemNetId(player, origin) == _dragging._originalGemNetId;
        }

        internal static void CancelDrag()
        {
            if (_dragging != null)
                _dragging.RemovePreview();
        }

        private void LateUpdate()
        {
            var player = DewPlayer.local;
            var status = ChaosSlotState.IsLocalPlayer(player) ? ChaosSlotState.FindBonus(player.hero) : null;
            var layout = ChaosSlotState.Read(status);
            bool visible = status != null && ChaosSlotState.TryMemorySlot(_slot.button.skillType, out var memory)
                && _slot.slotIndex >= SlotRules.BaseSlots && _slot.slotIndex < SlotRules.BaseSlots + layout[memory];
            _icon.enabled = visible;
            _hitbox.raycastTarget = visible && ChaosSlotState.CanGrab(player, _slot.thisSlotLocation);
            if (!_hitbox.raycastTarget || (_dragging == this && !TryGetDragOrigin(out _)))
                RemovePreview();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !_hitbox.raycastTarget
                || !ChaosSlotState.CanGrab(DewPlayer.local, _slot.thisSlotLocation))
                return;

            _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (_canvas == null)
                return;

            CancelDrag();
            _originalGemNetId = ChaosSlotState.GetGemNetId(DewPlayer.local, _slot.thisSlotLocation);
            var obj = new GameObject("Chaos Slot Drag", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _dragPreview = (RectTransform)obj.transform;
            _dragPreview.SetParent(_canvas.transform, false);
            _dragPreview.sizeDelta = new Vector2(25f, 34f);
            var image = obj.GetComponent<Image>();
            image.sprite = _icon.sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var group = obj.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            _dragging = this;
            UpdatePreview(eventData);
        }

        public void OnDrag(PointerEventData eventData) => UpdatePreview(eventData);

        public void OnEndDrag(PointerEventData eventData)
        {
            GemLocation origin = default;
            bool validOrigin = _dragging == this && TryGetDragOrigin(out origin);
            RemovePreview();
            if (!validOrigin)
                return;
            var hit = eventData.pointerCurrentRaycast.gameObject;
            if (hit == null)
                return;

            var overSlot = hit.GetComponentInParent<UI_InGame_GemSlot>();
            var target = overSlot != null ? overSlot.button : hit.GetComponentInParent<UI_InGame_SkillButton>();
            if (target != null)
                ChaosSlotNetwork.RequestMove(DewPlayer.local, origin, target.skillType);
        }

        // Keep the activation and hold handlers on the parent essence slot from receiving icon presses.
        public void OnPointerDown(PointerEventData eventData) { }
        public void OnPointerUp(PointerEventData eventData) { }
        public void OnPointerClick(PointerEventData eventData) { }

        private void OnDisable() => RemovePreview();

        private void UpdatePreview(PointerEventData eventData)
        {
            if (_dragPreview == null)
                return;
            var root = (RectTransform)_canvas.transform;
            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(root, eventData.position, camera, out var position))
                _dragPreview.position = position;
        }

        private void RemovePreview()
        {
            if (_dragging == this)
                _dragging = null;
            if (_dragPreview != null)
            {
                _dragPreview.gameObject.SetActive(false);
                Destroy(_dragPreview.gameObject);
            }
            _dragPreview = null;
        }
    }
}
