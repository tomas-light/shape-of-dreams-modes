using ShapeOfDreamsMods;
using UnityEngine;

namespace EffectBreakdown
{
    public sealed class EffectBreakdownConfig : ModConfig
    {
        public bool hideItemNotifications;

        public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
        {
            var changed = new SafeAction();
            onChanged = changed;
            requestUpdate = new SafeAction();

            var row = DewGUI.CreateHorizontalLayoutGroup(parent).transform;
            string label = TooltipText.Get(DescriptionPatch.TooltipCulture(), "HideItemNotifications");
            Object.Instantiate(DewGUI.widgetTextLabel, row).SetExpandWidth(true).SetText(label);
            var toggle = Object.Instantiate(DewGUI.widgetToggleCheck, row);
            toggle.isChecked = hideItemNotifications;
            toggle.onIsCheckedChanged.AddListener(value =>
            {
                if (hideItemNotifications == value)
                    return;
                hideItemNotifications = value;
                changed.Invoke();
            });
            requestUpdate.Add(() => toggle.isChecked = hideItemNotifications);
        }
    }
}
