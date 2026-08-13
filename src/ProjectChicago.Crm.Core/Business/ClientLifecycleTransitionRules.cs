using ProjectChicago.Crm.Core.Models.DataModels.Entities;

namespace ProjectChicago.Crm.Core.Business;

// CLIENT-010..015's transition-graph decision, isolated from ClientBusiness.ChangeLifecycleStatusAsync
// so the orchestration method stays about orchestration rather than the ruleset itself
// (onion-boundaries.md: "Business owns domain rules, lifecycle invariants"). CLIENT-010 enumerates
// the six statuses but no requirement defines which transitions between them are legal; this is the
// narrowest reversible assumption available (CLAUDE.md Usage #5): every distinct pair of
// non-Archived statuses may transition freely in either direction, any non-Archived status may
// transition to Archived, and Archived itself is terminal within this use case - un-archiving is
// the separate "Restored" action AUDIT-003 names, not a status-to-status transition this rule set
// decides. Revisit if a future requirement defines the graph explicitly (CLAUDE.md Governance #8:
// a missing business decision is documented, not silently invented as a permanent rule).
public static class ClientLifecycleTransitionRules
{
    public static bool IsAllowed(ClientLifecycleStatus from, ClientLifecycleStatus to)
    {
        if (!Enum.IsDefined(from) || !Enum.IsDefined(to))
        {
            return false;
        }

        if (from == to)
        {
            return false;
        }

        return from != ClientLifecycleStatus.Archived;
    }
}
