using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ChaosSlots
{
    [HarmonyPatch(typeof(UI_InGame_GemSlot), "Awake")]
    internal static class ChaosGemMaterials
    {
        private static readonly AccessTools.FieldRef<UI_InGame_GemSlot, Material[]> OwnedMaterials =
            AccessTools.FieldRefAccess<UI_InGame_GemSlot, Material[]>("_materials");

        internal static void RestoreTemplates(GameObject original, GameObject clone)
        {
            // Cloning a live layout copies references to materials that can be mid-flash.
            // Restore the shared templates before the cloned slots run their own Awake.
            var templates = new Dictionary<Material, Material>();
            foreach (var slot in original.GetComponentsInChildren<UI_InGame_GemSlot>(true))
            {
                var owned = OwnedMaterials(slot);
                if (owned == null)
                    continue;
                for (int i = 0; i < owned.Length; i++)
                {
                    if (owned[i] != null)
                        templates[owned[i]] = slot.buttonSharedMaterials[i];
                }
            }

            foreach (var image in clone.GetComponentsInChildren<Image>(true))
            {
                var material = image.material;
                if (material != null && templates.TryGetValue(material, out var template))
                    image.material = template;
            }
        }

        private static void Postfix(UI_InGame_GemSlot __instance)
        {
            if (__instance.GetComponentInParent<ChaosBonusLayout>(true) == null)
                return;

            // The native Awake skips inactive images, including icon objects copied from empty slots.
            var owned = OwnedMaterials(__instance);
            foreach (var image in __instance.GetComponentsInChildren<Image>(true))
            {
                int index = Array.IndexOf(__instance.buttonSharedMaterials, image.material);
                if (index >= 0)
                    image.material = owned[index];
            }
        }
    }
}
