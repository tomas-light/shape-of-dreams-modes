using HarmonyLib;

namespace ChaosSlots
{
    public sealed class ChaosSlotsMod : ModBehaviour
    {
        private void Awake()
        {
            instance.isAlteringGameplay = true;
            ChaosSlotNetwork.Initialize();
            harmony.PatchAll(typeof(ChaosSlotsMod).Assembly);
        }

        private void Update() => ChaosSlotNetwork.Poll();

        private void OnDestroy()
        {
            ChaosSlotHandle.CancelDrag();
            ChaosSlotNetwork.ShutDown();
            ChaosGemGroups.RestoreAll();
            ChaosScoreboardSlots.RestoreAll();
            ChaosScoreboardFrame.RestoreAll();
            ChaosResultFrame.RestoreAll();
            ChaosResultSlots.RestoreAll();
            ChaosPetal.Release();
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
