namespace ProjectChicago.Crm.Core.Models.DataModels.Entities;

// TASK-015 is the canonical source for this value set; Project's ProjectPriority reuses the same
// values because Task did not exist yet when Project was implemented (see ProjectPriority's
// comment). Named TaskItemPriority, not TaskPriority, for symmetry with TaskItemStatus.
public enum TaskItemPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3,
}
