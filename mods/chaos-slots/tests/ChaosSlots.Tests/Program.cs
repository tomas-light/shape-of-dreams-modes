using ChaosSlots;
using ShapeOfDreamsMods;

(string Name, Action Run)[] tests =
[
    (nameof(RewardPromptsUseTheGameLanguage), RewardPromptsUseTheGameLanguage),
    (nameof(FreshRunCanGrantToAnyMemory), FreshRunCanGrantToAnyMemory),
    (nameof(FourthGrantCanGoToSameMemory), FourthGrantCanGoToSameMemory),
    (nameof(FullBudgetBlocksGrantsEvenToEmptyMemories), FullBudgetBlocksGrantsEvenToEmptyMemories),
    (nameof(DistributedBudgetStillBlocksFifthGrant), DistributedBudgetStillBlocksFifthGrant),
    (nameof(UnknownMemoryCannotReceiveGrant), UnknownMemoryCannotReceiveGrant),
    (nameof(MoveAtFullBudgetPreservesTotal), MoveAtFullBudgetPreservesTotal),
    (nameof(FullBudgetStillAllowsEveryOtherMemoryAsDropTarget), FullBudgetStillAllowsEveryOtherMemoryAsDropTarget),
    (nameof(MoveIdentifiesWhichSlotsToReposition), MoveIdentifiesWhichSlotsToReposition),
    (nameof(LastBonusMovesWithoutCompactingAnotherSlot), LastBonusMovesWithoutCompactingAnotherSlot),
    (nameof(AccumulatedSlotsCanAllMoveToOneMemory), AccumulatedSlotsCanAllMoveToOneMemory),
    (nameof(DropOnSourceOrInvalidTargetIsRejected), DropOnSourceOrInvalidTargetIsRejected),
    (nameof(OnlyExistingBonusSlotsCanMove), OnlyExistingBonusSlotsCanMove),
    (nameof(RestoredAllocationCanStillBeMoved), RestoredAllocationCanStillBeMoved),
    (nameof(SlotMoveRequestTests.PlayerBudgetsRemainIndependent), SlotMoveRequestTests.PlayerBudgetsRemainIndependent),
    (nameof(SlotMoveRequestTests.CanMoveAnEmptyBonusSlot), SlotMoveRequestTests.CanMoveAnEmptyBonusSlot),
    (nameof(SlotMoveRequestTests.InvalidSlotRequestsAreRejected), SlotMoveRequestTests.InvalidSlotRequestsAreRejected),
    (nameof(SlotMoveRequestTests.ChangedSourceCountRejectsTheRequest), SlotMoveRequestTests.ChangedSourceCountRejectsTheRequest),
    (nameof(SlotMoveRequestTests.ChangedDestinationCountRejectsTheRequest), SlotMoveRequestTests.ChangedDestinationCountRejectsTheRequest),
    (nameof(SlotMoveRequestTests.TheSourceGemMustStillMatch), SlotMoveRequestTests.TheSourceGemMustStillMatch),
    (nameof(SlotMoveRequestTests.ImmediateRetryCannotMoveASecondSlot), SlotMoveRequestTests.ImmediateRetryCannotMoveASecondSlot),
    (nameof(SlotMoveRequestTests.ARepeatedSequenceIsRejectedEvenWhenTheOldSnapshotMatches), SlotMoveRequestTests.ARepeatedSequenceIsRejectedEvenWhenTheOldSnapshotMatches),
    (nameof(SlotMoveRequestTests.UnregisteringAPeerKeepsItsLastSequence), SlotMoveRequestTests.UnregisteringAPeerKeepsItsLastSequence),
    (nameof(BonusSlotArcTests.NoBonusHasNoUpperArc), BonusSlotArcTests.NoBonusHasNoUpperArc),
    (nameof(BonusSlotArcTests.FourSlotsMirrorTheStockArc), BonusSlotArcTests.FourSlotsMirrorTheStockArc),
    (nameof(BonusSlotArcTests.FewerSlotsUseASymmetricCentralSubarc), BonusSlotArcTests.FewerSlotsUseASymmetricCentralSubarc),
    (nameof(BonusSlotArcTests.UpperArcCentersOnTheMemoryCircle), BonusSlotArcTests.UpperArcCentersOnTheMemoryCircle),
    (nameof(BonusSlotArcTests.ClearanceShiftsTheWholeArcWithoutFlattening), BonusSlotArcTests.ClearanceShiftsTheWholeArcWithoutFlattening),
    (nameof(BonusSlotArcTests.TwoThroughFourSlotsAreSpacedAndCentered), BonusSlotArcTests.TwoThroughFourSlotsAreSpacedAndCentered),
    (nameof(BonusSlotArcTests.RoundClearanceDoesNotLiftClearOuterSlots), BonusSlotArcTests.RoundClearanceDoesNotLiftClearOuterSlots),
    (nameof(BonusSlotArcTests.RoundClearanceOnlyLiftsAsFarAsNeeded), BonusSlotArcTests.RoundClearanceOnlyLiftsAsFarAsNeeded),
    (nameof(BonusSlotArcTests.RoundClearanceUsesWidenedPositions), BonusSlotArcTests.RoundClearanceUsesWidenedPositions),
    (nameof(ScoreboardSlotLayoutTests.FiveSlotMemoryKeepsItsBaseSlotsAndShowsBothBonuses), ScoreboardSlotLayoutTests.FiveSlotMemoryKeepsItsBaseSlotsAndShowsBothBonuses),
    (nameof(ScoreboardSlotLayoutTests.EverySupportedCapacitySelectsItsOwnGroup), ScoreboardSlotLayoutTests.EverySupportedCapacitySelectsItsOwnGroup),
    (nameof(ScoreboardSlotLayoutTests.ExpandedFrameFitsTheUpperArcAtEveryBonusCount), ScoreboardSlotLayoutTests.ExpandedFrameFitsTheUpperArcAtEveryBonusCount),
    (nameof(ScoreboardSlotLayoutTests.UpperArcClearsTheChargeBadge), ScoreboardSlotLayoutTests.UpperArcClearsTheChargeBadge),
    (nameof(ScoreboardSlotLayoutTests.UnmodifiedPlayerKeepsTheStockFourSlotGroup), ScoreboardSlotLayoutTests.UnmodifiedPlayerKeepsTheStockFourSlotGroup),
    (nameof(ScoreboardSlotLayoutTests.RowHeightFollowsTheTallestMemory), ScoreboardSlotLayoutTests.RowHeightFollowsTheTallestMemory),
    (nameof(ScoreboardSlotLayoutTests.OnlyRegisteredPeersUseTheExpandedFourSlotGroup), ScoreboardSlotLayoutTests.OnlyRegisteredPeersUseTheExpandedFourSlotGroup),
];

var failures = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {error.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
return failures == 0 ? 0 : 1;

static void RewardPromptsUseTheGameLanguage()
{
    var culture = TooltipText.GetCulture("ja-JP");
    Equal("記憶を選択 · 混沌スロット：2/4", TooltipText.Format(culture, "ChooseMemoryWithSlotCount", 2, 4), "reward counter");
    Equal("堕落した混沌のスロットが上限に達しました", TooltipText.Get(culture, "SlotLimit"), "slot limit");
}

static void FreshRunCanGrantToAnyMemory()
{
    var layout = new SlotLayout(0, 0, 0, 0);
    Equal(3, SlotRules.BaseSlots, "Base capacity per memory");
    Equal(4, SlotRules.BonusLimit, "Total bonus capacity");
    Equal(0, layout.Total, "Initial bonus count");
    foreach (var slot in Enum.GetValues<MemorySlot>())
        Equal(true, SlotRules.CanGrant(layout, slot), $"Initial grant to {slot}");
}

static void FourthGrantCanGoToSameMemory()
{
    var layout = new SlotLayout(3, 0, 0, 0);
    Equal(true, SlotRules.CanGrant(layout, MemorySlot.Q), "Fourth bonus can go to Q");
    Equal(true, SlotRules.CanGrant(layout, MemorySlot.R), "Fourth bonus can go to R");
}

static void FullBudgetBlocksGrantsEvenToEmptyMemories()
{
    var layout = new SlotLayout(4, 0, 0, 0);
    Equal(7, SlotRules.BaseSlots + layout[MemorySlot.Q], "Q capacity with four bonuses");
    foreach (var slot in Enum.GetValues<MemorySlot>())
        Equal(false, SlotRules.CanGrant(layout, slot), $"Fifth grant to {slot}");
}

static void DistributedBudgetStillBlocksFifthGrant()
{
    var layout = new SlotLayout(2, 1, 0, 1);
    Equal(4, layout.Total, "Distributed bonus count");
    Equal(false, SlotRules.CanGrant(layout, MemorySlot.E), "Fifth bonus cannot go to E");
}

static void UnknownMemoryCannotReceiveGrant()
{
    Equal(false, SlotRules.CanGrant(default, (MemorySlot)42), "Grant to an invalid memory");
}

static void MoveAtFullBudgetPreservesTotal()
{
    var before = new SlotLayout(2, 1, 0, 1);
    var move = PlanMove(before, MemorySlot.Q, 3, MemorySlot.E);

    AssertLayout(move.Layout, 1, 1, 1, 1);
    Equal(before.Total, move.Layout.Total, "Move must conserve the bonus budget");
}

static void FullBudgetStillAllowsEveryOtherMemoryAsDropTarget()
{
    var layout = new SlotLayout(2, 1, 0, 1);
    foreach (var destination in Enum.GetValues<MemorySlot>())
    {
        Equal(false, SlotRules.CanGrant(layout, destination), $"Grant to {destination} at the cap");
        var canMove = SlotRules.TryPlanMove(layout, MemorySlot.Q, 3, destination, out _);
        Equal(destination != MemorySlot.Q, canMove, $"Drop on {destination} at the cap");
    }
}

static void MoveIdentifiesWhichSlotsToReposition()
{
    var move = PlanMove(new SlotLayout(2, 1, 0, 0), MemorySlot.Q, 3, MemorySlot.W);

    Equal(4, move.SourceLastIndex, "Source slot that fills the selected slot's gap");
    Equal(4, move.DestinationIndex, "New slot goes after W's existing bonus");
}

static void LastBonusMovesWithoutCompactingAnotherSlot()
{
    var move = PlanMove(new SlotLayout(0, 0, 0, 1), MemorySlot.R, 3, MemorySlot.Q);

    Equal(3, move.SourceLastIndex, "Selected slot is already last");
    Equal(3, move.DestinationIndex, "First bonus goes after three base slots");
    AssertLayout(move.Layout, 1, 0, 0, 0);
}

static void AccumulatedSlotsCanAllMoveToOneMemory()
{
    var layout = new SlotLayout(1, 1, 1, 1);
    foreach (var source in new[] { MemorySlot.Q, MemorySlot.W, MemorySlot.R })
        layout = PlanMove(layout, source, 3, MemorySlot.E).Layout;

    AssertLayout(layout, 0, 0, 4, 0);
    Equal(7, SlotRules.BaseSlots + layout[MemorySlot.E], "Target capacity");
}

static void DropOnSourceOrInvalidTargetIsRejected()
{
    var layout = new SlotLayout(1, 0, 0, 0);
    Equal(false, SlotRules.TryPlanMove(layout, MemorySlot.Q, 3, MemorySlot.Q, out _), "Drop on source");
    Equal(false, SlotRules.TryPlanMove(layout, MemorySlot.Q, 3, (MemorySlot)42, out _), "Drop outside memories");
    Equal(false, SlotRules.TryPlanMove(layout, (MemorySlot)42, 3, MemorySlot.Q, out _), "Invalid source");
}

static void OnlyExistingBonusSlotsCanMove()
{
    var layout = new SlotLayout(1, 0, 0, 0);
    foreach (var index in new[] { -1, 0, 1, 2, 4 })
        Equal(false, SlotRules.TryPlanMove(layout, MemorySlot.Q, index, MemorySlot.W, out _), $"Q slot {index}");

    Equal(false, SlotRules.TryPlanMove(layout, MemorySlot.E, 3, MemorySlot.W, out _), "E has no bonus");
}

static void RestoredAllocationCanStillBeMoved()
{
    // These are the four bonus counters supplied by the game's run save.
    var fromSave = new SlotLayout(0, 2, 1, 1);
    Equal(false, SlotRules.CanGrant(fromSave, MemorySlot.Q), "Restored budget remains full");

    var move = PlanMove(fromSave, MemorySlot.W, 4, MemorySlot.Q);
    AssertLayout(move.Layout, 1, 1, 1, 1);
    Equal(3, move.DestinationIndex, "First bonus position after continuing the run");
}

static SlotMove PlanMove(SlotLayout layout, MemorySlot source, int index, MemorySlot destination)
{
    Equal(true, SlotRules.TryPlanMove(layout, source, index, destination, out var move),
        $"Move slot {index} from {source} to {destination}");
    return move;
}

static void AssertLayout(SlotLayout actual, int q, int w, int e, int r)
{
    Equal(q, actual[MemorySlot.Q], "Q bonus count");
    Equal(w, actual[MemorySlot.W], "W bonus count");
    Equal(e, actual[MemorySlot.E], "E bonus count");
    Equal(r, actual[MemorySlot.R], "R bonus count");
}

static void Equal<T>(T expected, T actual, string context)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{context}: expected {expected}, got {actual}");
}
