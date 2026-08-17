using Microsoft.EntityFrameworkCore;
using TG.Payroll.Web.Models;

namespace TG.Payroll.Web.Data;

public sealed class PayrollDbContext(DbContextOptions<PayrollDbContext> options) : DbContext(options)
{
    public DbSet<PayrollUser> Users => Set<PayrollUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<PayrollUser>();
        user.ToTable("USERS");
        user.HasKey(entity => entity.UserId);
        user.Property(entity => entity.UserId).HasColumnName("USER_ID").HasColumnType("NUMBER(10)").ValueGeneratedNever();
        user.Property(entity => entity.UserName).HasColumnName("USER_NAME").HasColumnType("VARCHAR2(80)").HasMaxLength(80);
        user.Property(entity => entity.Admin).HasColumnName("ADMIN").HasColumnType("NUMBER");
        user.Property(entity => entity.CallingId).HasColumnName("CALLING_ID").HasColumnType("NUMBER");
        user.Property(entity => entity.Password).HasColumnName("PASSWORD").HasColumnType("VARCHAR2(50)").HasMaxLength(50);
        user.Property(entity => entity.CompanyId).HasColumnName("COMPANY_ID").HasColumnType("VARCHAR2(50)").HasMaxLength(50);
        user.Property(entity => entity.LoginStatus).HasColumnName("LOGIN_STATUS").HasColumnType("NUMBER");
        user.Property(entity => entity.IsLock).HasColumnName("IS_LOCK").HasColumnType("NUMBER");
        user.Property(entity => entity.EmpId).HasColumnName("EMP_ID").HasColumnType("NUMBER(10)");
        user.Property(entity => entity.EmpCode).HasColumnName("EMP_CODE").HasColumnType("VARCHAR2(15)").HasMaxLength(15);
        user.Property(entity => entity.Remarks).HasColumnName("REMARKS").HasColumnType("VARCHAR2(50)").HasMaxLength(50);
        user.Property(entity => entity.Photo).HasColumnName("PHOTO").HasColumnType("VARCHAR2(100)").HasMaxLength(100);
        user.Property(entity => entity.PrivilegeArray).HasColumnName("PRIVILEGE_ARRAY").HasColumnType("VARCHAR2(4000)").HasMaxLength(4000);
        user.Property(entity => entity.DefaultMenuCalander).HasColumnName("DEFAULT_MENU_CALANDER").HasColumnType("VARCHAR2(1)").HasMaxLength(1);
    }
}
