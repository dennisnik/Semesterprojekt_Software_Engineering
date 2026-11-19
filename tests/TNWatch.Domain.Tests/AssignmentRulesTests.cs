using TNWatch.Domain.Assignments;

namespace TNWatch.Domain.Tests;

public class AssignmentRulesTests
{
    private static readonly UserRef Kevin = new("kevin", "Kevin");
    private static readonly UserRef Anja = new("anja", "Anja");
    private static readonly UserRef Markus = new("markus", "Markus");
    private static readonly DateTimeOffset T0 = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);

    private static NodeAssignment HeldBy(UserRef user) => new("TN0001", user, user, T0);

    [Fact]
    public void Anyone_can_claim_a_free_node()
        => Assert.Equal(AssignmentOutcome.Ok, AssignmentRules.CheckClaim(null, Kevin, actorIsManager: false));

    [Fact]
    public void Claiming_own_node_is_unchanged_and_user_names_are_case_insensitive()
        => Assert.Equal(AssignmentOutcome.Unchanged, AssignmentRules.CheckClaim(HeldBy(Kevin), new UserRef("KEVIN", "Kevin"), false));

    [Fact]
    public void Technician_cannot_take_over_a_colleagues_node_but_manager_can()
    {
        Assert.Equal(AssignmentOutcome.AlreadyAssigned, AssignmentRules.CheckClaim(HeldBy(Kevin), Anja, actorIsManager: false));
        Assert.Equal(AssignmentOutcome.Ok, AssignmentRules.CheckClaim(HeldBy(Kevin), Markus, actorIsManager: true));
    }

    [Fact]
    public void Only_manager_assigns_others()
    {
        Assert.Equal(AssignmentOutcome.NotAllowed, AssignmentRules.CheckAssign(null, Kevin, false, Anja));
        Assert.Equal(AssignmentOutcome.Ok, AssignmentRules.CheckAssign(HeldBy(Kevin), Markus, true, Anja));
        Assert.Equal(AssignmentOutcome.Unchanged, AssignmentRules.CheckAssign(HeldBy(Anja), Markus, true, Anja));
    }

    [Fact]
    public void Assigning_oneself_behaves_like_claiming()
    {
        Assert.Equal(AssignmentOutcome.Ok, AssignmentRules.CheckAssign(null, Kevin, false, Kevin));
        Assert.Equal(AssignmentOutcome.AlreadyAssigned, AssignmentRules.CheckAssign(HeldBy(Anja), Kevin, false, Kevin));
    }

    [Fact]
    public void Release_rules()
    {
        Assert.Equal(AssignmentOutcome.NotFound, AssignmentRules.CheckRelease(null, Kevin, false));
        Assert.Equal(AssignmentOutcome.Ok, AssignmentRules.CheckRelease(HeldBy(Kevin), Kevin, false));
        Assert.Equal(AssignmentOutcome.NotAllowed, AssignmentRules.CheckRelease(HeldBy(Kevin), Anja, false));
        Assert.Equal(AssignmentOutcome.Ok, AssignmentRules.CheckRelease(HeldBy(Kevin), Markus, true));
    }

    [Theory]
    [InlineData("kevin", "Kevin", "K")]
    [InlineData("amueller", "Anja Müller", "AM")]
    [InlineData("x", "  Anna   Lena Berg ", "AL")]
    [InlineData("kevin", "", "K")]
    [InlineData("", "", "?")]
    public void Initials_for_the_map_badge(string userName, string displayName, string expected)
        => Assert.Equal(expected, new UserRef(userName, displayName).Initials);

    [Fact]
    public void Self_claim_is_detected()
    {
        Assert.True(HeldBy(Kevin).IsSelfClaimed);
        Assert.False(new NodeAssignment("TN0001", Kevin, Markus, T0).IsSelfClaimed);
        Assert.Throws<ArgumentNullException>(() => AssignmentRules.CheckClaim(null, null!, false));
        Assert.Throws<ArgumentNullException>(() => AssignmentRules.CheckAssign(null, Kevin, true, null!));
        Assert.Throws<ArgumentNullException>(() => AssignmentRules.CheckRelease(null, null!, false));
    }
}
