using System;
using System.Collections.Generic;

namespace highlands.Models;

public partial class User
{
    public int UserId { get; set; }

    public string UserName { get; set; } = null!;

    public string? Password { get; set; }

    public string? Email { get; set; }

    public string? Role { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? RoleId { get; set; }

    public string? Type { get; set; }

    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual Role? RoleNavigation { get; set; }
}
