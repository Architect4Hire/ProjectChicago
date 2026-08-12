namespace ProjectChicago.Crm.Core.Models.DataModels.Entities;

// The initial CRM Project statuses (PROJECT-010). A Project has exactly one current status at a
// time; Archived is a status value rather than a separate flag (mirrors ClientLifecycleStatus), so
// "current status" and "archived state" can never disagree (PROJECT-014).
public enum ProjectStatus
{
    Planned = 0,
    Active = 1,
    OnHold = 2,
    Completed = 3,
    Cancelled = 4,
    Archived = 5,
}
