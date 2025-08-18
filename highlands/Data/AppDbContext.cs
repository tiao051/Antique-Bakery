using highlands.Models;
using Microsoft.EntityFrameworkCore;

namespace highlands.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Attendance> Attendances { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<Feedback> Feedbacks { get; set; }

    public virtual DbSet<Ingredient> Ingredients { get; set; }

    public virtual DbSet<Inventory> Inventories { get; set; }

    public virtual DbSet<Level> Levels { get; set; }

    public virtual DbSet<MenuItem> MenuItems { get; set; }

    public virtual DbSet<MenuItemPrice> MenuItemPrices { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderDetail> OrderDetails { get; set; }

    public virtual DbSet<OrderInventory> OrderInventories { get; set; }

    public virtual DbSet<OrderPayment> OrderPayments { get; set; }

    public virtual DbSet<Promotion> Promotions { get; set; }

    public virtual DbSet<Recipe> Recipes { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Salary> Salaries { get; set; }

    public virtual DbSet<Sale> Sales { get; set; }

    public virtual DbSet<Shift> Shifts { get; set; }

    public virtual DbSet<ShiftInventory> ShiftInventories { get; set; }

    public virtual DbSet<StockIn> StockIns { get; set; }

    public virtual DbSet<StockOut> StockOuts { get; set; }

    public virtual DbSet<Store> Stores { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<TransactionHistory> TransactionHistories { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=tiao;Database=coffee_shop;User Id=sa;Password=Your_password123;TrustServerCertificate=True;Encrypt=False;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.HasKey(e => e.AttendanceId).HasName("PK__Attendan__8B69261CE8C56896");

            entity.ToTable("Attendance");

            entity.Property(e => e.InTime).HasColumnType("datetime");
            entity.Property(e => e.OutTime).HasColumnType("datetime");

            entity.HasOne(d => d.Employee).WithMany(p => p.Attendances)
                .HasForeignKey(d => d.EmployeeId)
                .HasConstraintName("FK__Attendanc__Emplo__5BE2A6F2");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId).HasName("PK__Customer__A4AE64D83C502877");

            entity.ToTable("Customer");

            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Message).HasMaxLength(50);
            entity.Property(e => e.Phone).HasMaxLength(15);

            //entity.HasOne(d => d.User).WithMany(p => p.Customers)
            //    .HasForeignKey(d => d.UserId)
            //    .HasConstraintName("FK__Customer__UserId__3D5E1FD2");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.EmployeeId).HasName("PK__Employee__7AD04F1164043082");

            entity.ToTable("Employee");

            entity.Property(e => e.DateOfHire).HasColumnType("datetime");
            entity.Property(e => e.Position).HasMaxLength(50);

            entity.HasOne(d => d.Level).WithMany(p => p.Employees)
                .HasForeignKey(d => d.LevelId)
                .HasConstraintName("FK__Employee__LevelI__5629CD9C");

            entity.HasOne(d => d.User).WithMany(p => p.Employees)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Employee__UserId__5535A963");
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("PK__Feedback__6A4BEDD64013EEB3");

            entity.ToTable("Feedback");

            entity.Property(e => e.Content).HasMaxLength(255);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            //entity.HasOne(d => d.Customer).WithMany(p => p.Feedbacks)
            //    .HasForeignKey(d => d.CustomerId)
            //    .HasConstraintName("FK__Feedback__Custom__4E88ABD4");
        });

        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(e => e.IngredientId).HasName("PK__Ingredie__BEAEB27AC38E192A");

            entity.ToTable("Ingredient");

            entity.HasIndex(e => e.IngredientName, "UQ__Ingredie__A1B2F1CC622751A2").IsUnique();

            entity.Property(e => e.IngredientId).HasColumnName("IngredientID");
            entity.Property(e => e.IngredientCategory).HasMaxLength(50);
            entity.Property(e => e.IngredientName).HasMaxLength(255);
            entity.Property(e => e.IngredientType).HasMaxLength(50);
            entity.Property(e => e.Unit).HasMaxLength(50);
        });

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasKey(e => e.InventoryId).HasName("PK__Inventor__F5FDE6B310FE7243");

            entity.ToTable("Inventory");

            entity.Property(e => e.ItemName).HasMaxLength(100);
            entity.Property(e => e.Price).HasColumnType("decimal(10, 2)");
        });

        modelBuilder.Entity<Level>(entity =>
        {
            entity.HasKey(e => e.LevelId).HasName("PK__Level__09F03C262716CD77");

            entity.ToTable("Level");

            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.LevelName).HasMaxLength(50);
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(e => e.ItemName);

            entity.ToTable("MenuItem");

            entity.Property(e => e.ItemName).HasMaxLength(255);
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.SubCategory).HasMaxLength(50);
            entity.Property(e => e.Type).HasMaxLength(255);
        });

        modelBuilder.Entity<MenuItemPrice>(entity =>
        {
            entity.HasKey(e => new { e.ItemName, e.Size });

            entity.ToTable("MenuItemPrice");

            entity.Property(e => e.ItemName).HasMaxLength(255);
            entity.Property(e => e.Size).HasMaxLength(50);
            entity.Property(e => e.Price).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.ItemNameNavigation).WithMany(p => p.MenuItemPrices)
                .HasForeignKey(d => d.ItemName)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__MenuItemP__ItemN__30C33EC3");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Order__C3905BCFC7AC295F");

            entity.ToTable("Order");

            entity.Property(e => e.OrderDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(10, 2)");

            //entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
            //    .HasForeignKey(d => d.CustomerId)
            //    .HasConstraintName("FK__Order__CustomerI__4222D4EF");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.OrderDetailId).HasName("PK__OrderDet__D3B9D36CCBC8E7B5");

            entity.ToTable("OrderDetail");

            entity.Property(e => e.ItemName).HasMaxLength(255);
            entity.Property(e => e.Price).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.ItemNameNavigation).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.ItemName)
                .HasConstraintName("FK_OrderDetails_MenuItem");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__OrderDeta__Order__49C3F6B7");
        });

        modelBuilder.Entity<OrderInventory>(entity =>
        {
            entity.HasKey(e => e.OrderInventoryId).HasName("PK__OrderInv__6119233E131C7EBF");

            entity.ToTable("OrderInventory");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderInventories)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__OrderInve__Order__7C4F7684");

            entity.HasOne(d => d.ShiftInventory).WithMany(p => p.OrderInventories)
                .HasForeignKey(d => d.ShiftInventoryId)
                .HasConstraintName("FK__OrderInve__Shift__7D439ABD");
        });

        modelBuilder.Entity<OrderPayment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__OrderPay__9B556A38239F6466");

            entity.ToTable("OrderPayment");

            entity.Property(e => e.PaymentAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.PaymentDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.PaymentMethod).HasMaxLength(50);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderPayments)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__OrderPaym__Order__6E01572D");
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasKey(e => e.PromotionId).HasName("PK__Promotio__52C42FCF0598FB73");

            entity.ToTable("Promotion");

            entity.Property(e => e.Discount).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.PromoCode).HasMaxLength(50);
            entity.Property(e => e.StartDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(e => e.RecipeId).HasName("PK__Recipe__FDD988D0C6A8C1DC");

            entity.ToTable("Recipe");

            entity.Property(e => e.RecipeId).HasColumnName("RecipeID");
            entity.Property(e => e.IngredientId).HasColumnName("IngredientID");
            entity.Property(e => e.ItemName).HasMaxLength(255);
            entity.Property(e => e.Quantity).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Size).HasMaxLength(50);

            entity.HasOne(d => d.Ingredient).WithMany(p => p.Recipes)
                .HasForeignKey(d => d.IngredientId)
                .HasConstraintName("FK__Recipe__Ingredie__2DE6D218");

            entity.HasOne(d => d.ItemNameNavigation).WithMany(p => p.Recipes)
                .HasForeignKey(d => d.ItemName)
                .HasConstraintName("FK__Recipe__ItemName__2CF2ADDF");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Role__8AFACE1A61B4E4D7");

            entity.ToTable("Role");

            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<Salary>(entity =>
        {
            entity.HasKey(e => e.SalaryId).HasName("PK__Salary__4BE2045792994C8F");

            entity.ToTable("Salary");

            entity.Property(e => e.BaseSalary).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Bonus).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Deductions).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.MonthYear).HasColumnType("datetime");
            entity.Property(e => e.TotalSalary).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Employee).WithMany(p => p.Salaries)
                .HasForeignKey(d => d.EmployeeId)
                .HasConstraintName("FK__Salary__Employee__59063A47");
        });

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(e => e.SalesId).HasName("PK__Sales__C952FB32A5EA0565");

            entity.Property(e => e.SalesDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Promotion).WithMany(p => p.Sales)
                .HasForeignKey(d => d.PromotionId)
                .HasConstraintName("FK__Sales__Promotion__66603565");

            entity.HasOne(d => d.Store).WithMany(p => p.Sales)
                .HasForeignKey(d => d.StoreId)
                .HasConstraintName("FK__Sales__StoreId__656C112C");
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(e => e.ShiftId).HasName("PK__Shift__C0A83881F64479A8");

            entity.ToTable("Shift");

            entity.Property(e => e.EndTime).HasColumnType("datetime");
            entity.Property(e => e.StartTime).HasColumnType("datetime");

            entity.HasOne(d => d.Employee).WithMany(p => p.Shifts)
                .HasForeignKey(d => d.EmployeeId)
                .HasConstraintName("FK__Shift__EmployeeI__75A278F5");
        });

        modelBuilder.Entity<ShiftInventory>(entity =>
        {
            entity.HasKey(e => e.ShiftInventoryId).HasName("PK__ShiftInv__2DCA0146D81CBAB1");

            entity.ToTable("ShiftInventory");

            entity.HasOne(d => d.Inventory).WithMany(p => p.ShiftInventories)
                .HasForeignKey(d => d.InventoryId)
                .HasConstraintName("FK__ShiftInve__Inven__797309D9");

            entity.HasOne(d => d.Shift).WithMany(p => p.ShiftInventories)
                .HasForeignKey(d => d.ShiftId)
                .HasConstraintName("FK__ShiftInve__Shift__787EE5A0");
        });

        modelBuilder.Entity<StockIn>(entity =>
        {
            entity.HasKey(e => e.StockInId).HasName("PK__StockIn__794DA66CC53C54D7");

            entity.ToTable("StockIn");

            entity.Property(e => e.Price).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.StockInDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Inventory).WithMany(p => p.StockIns)
                .HasForeignKey(d => d.InventoryId)
                .HasConstraintName("FK__StockIn__Invento__02084FDA");

            entity.HasOne(d => d.Store).WithMany(p => p.StockIns)
                .HasForeignKey(d => d.StoreId)
                .HasConstraintName("FK__StockIn__StoreId__02FC7413");

            entity.HasOne(d => d.Supplier).WithMany(p => p.StockIns)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("FK__StockIn__Supplie__01142BA1");
        });

        modelBuilder.Entity<StockOut>(entity =>
        {
            entity.HasKey(e => e.StockOutId).HasName("PK__StockOut__C5308D7A449F24AB");

            entity.ToTable("StockOut");

            entity.Property(e => e.ItemName).HasMaxLength(100);
            entity.Property(e => e.Price).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.StockOutDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Inventory).WithMany(p => p.StockOuts)
                .HasForeignKey(d => d.InventoryId)
                .HasConstraintName("FK__StockOut__Invent__71D1E811");

            entity.HasOne(d => d.Store).WithMany(p => p.StockOuts)
                .HasForeignKey(d => d.StoreId)
                .HasConstraintName("FK__StockOut__StoreI__72C60C4A");
        });

        modelBuilder.Entity<Store>(entity =>
        {
            entity.HasKey(e => e.StoreId).HasName("PK__Store__3B82F10127480322");

            entity.ToTable("Store");

            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(15);
            entity.Property(e => e.StoreName).HasMaxLength(100);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupplierId).HasName("PK__Supplier__4BE666B4D1C7FA1E");

            entity.ToTable("Supplier");

            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.Contact).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(15);
            entity.Property(e => e.SupplierName).HasMaxLength(100);
        });

        modelBuilder.Entity<TransactionHistory>(entity =>
        {
            entity.HasKey(e => e.TransactionId).HasName("PK__Transact__55433A6B5B0D7DFA");

            entity.ToTable("TransactionHistory");

            entity.Property(e => e.Amount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.PaymentMethod).HasMaxLength(50);
            entity.Property(e => e.TransactionDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Order).WithMany(p => p.TransactionHistories)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__Transacti__Order__6A30C649");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C8E46F69C");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .HasDefaultValue("Customer");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("Local");
            entity.Property(e => e.UserName).HasMaxLength(100);

            entity.HasOne(d => d.RoleNavigation).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK__Users__RoleId__3A81B327");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
