namespace ProjectChicago.Crm.Core.Models.DataModels.Entities;

// PROJECT-002 requires a Priority field but does not define its own value set; TASK-015 is the
// only priority scale the requirements define, so Project reuses it here (narrowest reversible
// assumption - revisit if a Project-specific scale is ever specified).
public enum ProjectPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3,
}
