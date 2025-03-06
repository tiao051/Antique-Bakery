using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class Shift
{
    public int ShiftId { get; set; }

    public int? EmployeeId { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public DateOnly? Date { get; set; }

    public virtual Employee? Employee { get; set; }

    public virtual ICollection<ShiftInventory> ShiftInventories { get; set; } = new List<ShiftInventory>();
}
