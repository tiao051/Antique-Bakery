using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class Salary
{
    public int SalaryId { get; set; }

    public int? EmployeeId { get; set; }

    public decimal? BaseSalary { get; set; }

    public decimal? Bonus { get; set; }

    public decimal? Deductions { get; set; }

    public decimal? TotalSalary { get; set; }

    public DateTime? MonthYear { get; set; }

    public virtual Employee? Employee { get; set; }
}
