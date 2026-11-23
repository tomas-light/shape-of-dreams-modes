namespace ChaosSlots.QaRoom
{
    public sealed class TestRoomMod : ModBehaviour
    {
        private void Awake()
        {
            instance.isAlteringGameplay = true;
            harmony.PatchAll(typeof(TestRoomMod).Assembly);
        }

        private void OnDestroy() => harmony.UnpatchAll(harmony.Id);
    }
}
