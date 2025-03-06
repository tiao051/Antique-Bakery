using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class Employee
{
    public int EmployeeId { get; set; }

    public int? UserId { get; set; }

    public string? Position { get; set; }

    public int? StoreId { get; set; }

    public DateTime? DateOfHire { get; set; }

    public int? LevelId { get; set; }

    public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();

    public virtual Level? Level { get; set; }

    public virtual ICollection<Salary> Salaries { get; set; } = new List<Salary>();

    public virtual ICollection<Shift> Shifts { get; set; } = new List<Shift>();

    public virtual User? User { get; set; }
}
