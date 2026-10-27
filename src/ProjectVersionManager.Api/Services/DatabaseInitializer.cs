using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectVersionManager.Api.Data;
using ProjectVersionManager.Api.Entities;

namespace ProjectVersionManager.Api.Services;

public sealed class DatabaseInitializer(
    PvmDbContext db,
    IConfiguration configuration,
    ILogger<DatabaseInitializer> logger)
{
    public async Task EnsureDevelopmentAdminAsync()
    {
        try
        {
            var password = configuration["DevelopmentAdmin:Password"] ?? "admin123";
            var hasher = new PasswordHasher<PvmUser>();
            var admins = await db.Users.Where(x => x.Username == "admin").ToListAsync();
            var admin = admins.SingleOrDefault();
            if (admin is null)
            {
                admin = new PvmUser
                {
                    Username = "admin",
                    DisplayName = "系统管理员",
                    RoleCode = "ADMIN",
                    IsEnabled = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                admin.PasswordHash = hasher.HashPassword(admin, password);
                db.Users.Add(admin);
                await db.SaveChangesAsync();
                logger.LogInformation("已创建开发环境管理员账号 admin。");
                return;
            }

            try
            {
                _ = hasher.VerifyHashedPassword(admin, admin.PasswordHash, password);
            }
            catch (FormatException)
            {
                admin.PasswordHash = hasher.HashPassword(admin, password);
                admin.RoleCode = "ADMIN";
                admin.IsEnabled = true;
                admin.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();
                logger.LogWarning("开发环境 admin 原密码不是有效哈希，已转换为 ASP.NET Core 密码哈希。");
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "开发管理员初始化失败；应用继续启动，请确认 SQL Server 连接和数据表。");
        }
    }
}
