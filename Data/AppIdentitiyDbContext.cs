using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace DotNetCoreAuthorizationServer.Data
{
    public class AppIdentitiyDbContext : IdentityDbContext<AppIdentityUser, AppIdentityRole, Guid>
    {

        public AppIdentitiyDbContext(DbContextOptions<AppIdentitiyDbContext> options)
            : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppIdentityUser>(b =>
            {
                b.Property(u => u.Id);
            });

            modelBuilder.Entity<AppIdentityRole>(b =>
            {
                b.Property(u => u.Id);
            });

            modelBuilder.Entity<AppIdentityUser>().ToTable("User");
            modelBuilder.Entity<AppIdentityRole>().ToTable("Role");
            modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("UserRole");
            modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogin");
            modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaim");
            modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaim");
            modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserToken");
        }

        public static async Task Initialize(AppIdentitiyDbContext context, UserManager<AppIdentityUser> userManager)
        {
            context.Database.EnsureCreated();

            if (context.Users.Any())
                return;

            var user = new AppIdentityUser()
            {
                UserName = "max.muster@mustermann.de",
                Email = "max.muster@mustermann.de",
                FirstName = "Max",
                LastName = "Muster",
            };
            var result = await userManager.CreateAsync(user, "S3cr3tPa55");
            if (!result.Succeeded)
                return;

            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await userManager.ConfirmEmailAsync(user, token);

            // context.SaveChanges();
        }
    }
}
