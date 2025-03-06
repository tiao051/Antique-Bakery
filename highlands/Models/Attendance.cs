using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class Attendance
{
    public int AttendanceId { get; set; }

    public int? EmployeeId { get; set; }

    public DateOnly? Date { get; set; }

    public DateTime? InTime { get; set; }

    public DateTime? OutTime { get; set; }

    public virtual Employee? Employee { get; set; }
}
