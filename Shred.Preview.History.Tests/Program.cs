using osu.Game.Rulesets;
Revision51HistoryChecks.Run();
sealed class Store(RulesetInfo shred) : RulesetStore
{
    public override IEnumerable<RulesetInfo> AvailableRulesets => new[] { new RulesetInfo { ShortName="mania", Name="mania", OnlineID=3, Available=true, InstantiationInfo=shred.InstantiationInfo }, shred };
}
