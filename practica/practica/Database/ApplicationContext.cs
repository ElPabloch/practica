using Microsoft.EntityFrameworkCore;
public class ApplicationContext : DbContext
{
    public DbSet<User> List_User { get; set; } = null!;
    public DbSet<Admin> List_Admin { get; set; } = null!;

    public ApplicationContext(DbContextOptions<ApplicationContext> options) : base(options)
    {
    }
    // ��������:
    //Add-Migration �����_��������
    //Update-Database
}
