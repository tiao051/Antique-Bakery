using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class Level
{
    public int LevelId { get; set; }

    public string? LevelName { get; set; }

    public string? Description { get; set; }

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
