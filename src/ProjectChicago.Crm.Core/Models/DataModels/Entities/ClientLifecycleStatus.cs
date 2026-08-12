namespace ProjectChicago.Crm.Core.Models.DataModels.Entities;

// The initial CRM Client lifecycle statuses (CLIENT-010). A Client has exactly one current status
// at a time; Archived is a status value rather than a separate flag, so "current status" and
// "archived state" can never disagree (CLIENT-013/CLIENT-014).
public enum ClientLifecycleStatus
{
    Lead = 0,
    Prospect = 1,
    Active = 2,
    OnHold = 3,
    Inactive = 4,
    Archived = 5,
}
