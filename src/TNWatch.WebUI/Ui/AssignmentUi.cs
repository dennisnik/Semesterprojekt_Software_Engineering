using TNWatch.Domain.Assignments;

namespace TNWatch.WebUI.Ui;

/// <summary>Which assignment buttons to show; mirrors <see cref="AssignmentRules"/> so the UI never offers a forbidden action.</summary>
public static class AssignmentUi
{
    public static bool CanClaim(NodeAssignment? current, UserRef user, bool isManager) =>
        AssignmentRules.CheckClaim(current, user, isManager) == AssignmentOutcome.Ok;

    public static bool CanRelease(NodeAssignment? current, UserRef user, bool isManager) =>
        AssignmentRules.CheckRelease(current, user, isManager) == AssignmentOutcome.Ok;
}
