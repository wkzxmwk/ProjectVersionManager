using Microsoft.EntityFrameworkCore;
using ProjectVersionManager.Api.Entities;

namespace ProjectVersionManager.Api.Data;

public sealed class PvmDbContext(DbContextOptions<PvmDbContext> options) : DbContext(options)
{
    public DbSet<PvmUser> Users => Set<PvmUser>();
    public DbSet<ProjectVersion> ProjectVersions => Set<ProjectVersion>();
    public DbSet<OperationLog> OperationLogs => Set<OperationLog>();
    public DbSet<ProjectOption> ProjectOptions => Set<ProjectOption>();
    public DbSet<PvmUserPermission> UserPermissions => Set<PvmUserPermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PvmUser>(entity =>
        {
            entity.ToTable("PVM_USER", table =>
            {
                table.HasCheckConstraint("CK_PVM_USER_ENABLED", "[IS_ENABLED] IN (0, 1)");
                table.HasCheckConstraint("CK_PVM_USER_ROLE", "[ROLE_CODE] IN ('ADMIN', 'USER')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("ID").HasMaxLength(36);
            entity.Property(x => x.Username).HasColumnName("USERNAME").HasMaxLength(100).IsRequired();
            entity.Property(x => x.PasswordHash).HasColumnName("PASSWORD_HASH").HasMaxLength(500).IsRequired();
            entity.Property(x => x.DisplayName).HasColumnName("DISPLAY_NAME").HasMaxLength(100);
            entity.Property(x => x.RoleCode).HasColumnName("ROLE_CODE").HasMaxLength(50).HasDefaultValue("USER").IsRequired();
            entity.Property(x => x.StaffGroup).HasColumnName("STAFF_GROUP").HasMaxLength(100);
            entity.Property(x => x.PersonnelGroup).HasColumnName("PERSONNEL_GROUP").HasMaxLength(100);
            entity.Property(x => x.IsEnabled).HasColumnName("IS_ENABLED").HasColumnType("bit").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<ProjectVersion>(entity =>
        {
            entity.ToTable("PVM_PROJECT_VERSION", table =>
            {
                table.HasCheckConstraint("CK_PVM_PV_STATUS", "[STATUS] IN ('PENDING', 'WAIT_COMPILE', 'COMPILED', 'PRE_RELEASED', 'RELEASED', 'CANCELED', 'COMMENTED')");
                table.HasCheckConstraint("CK_PVM_PV_DELETED", "[IS_DELETED] IN (0, 1)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("ID").HasMaxLength(36);
            entity.Property(x => x.ProjectDate).HasColumnName("PROJECT_DATE").HasColumnType("datetime2");
            entity.Property(x => x.ProjectType).HasColumnName("PROJECT_TYPE").HasMaxLength(50).IsRequired();
            entity.Property(x => x.Developer).HasColumnName("DEVELOPER").HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProgramGroup).HasColumnName("PROGRAM_GROUP").HasMaxLength(100).IsRequired();
            entity.Property(x => x.PersonnelGroup).HasColumnName("PERSONNEL_GROUP").HasMaxLength(100);
            entity.Property(x => x.ProgramName).HasColumnName("PROGRAM_NAME").HasMaxLength(200).IsRequired();
            entity.Property(x => x.ProgramVersionNumber).HasColumnName("PROGRAM_VERSION").HasMaxLength(50).IsRequired();
            entity.Property(x => x.Compiler).HasColumnName("COMPILER").HasMaxLength(100);
            entity.Property(x => x.CompileTime).HasColumnName("COMPILE_TIME").HasColumnType("datetime2");
            entity.Property(x => x.CompiledProgramVersion).HasColumnName("COMPILED_PROGRAM_VERSION").HasMaxLength(200);
            entity.Property(x => x.VersionDescription).HasColumnName("VERSION_DESCRIPTION").HasMaxLength(2000);
            entity.Property(x => x.ReleaseTime).HasColumnName("RELEASE_TIME").HasColumnType("datetime2");
            entity.Property(x => x.BranchName).HasColumnName("BRANCH_NAME").HasMaxLength(300);
            entity.Property(x => x.Remark1).HasColumnName("REMARK1").HasMaxLength(2000);
            entity.Property(x => x.Remark2).HasColumnName("REMARK2").HasMaxLength(2000);
            entity.Property(x => x.Status).HasColumnName("STATUS").HasMaxLength(30).HasDefaultValue("PENDING").IsRequired();
            entity.Property(x => x.CreatedBy).HasColumnName("CREATED_BY").HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(x => x.UpdatedBy).HasColumnName("UPDATED_BY").HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(x => x.IsDeleted).HasColumnName("IS_DELETED").HasColumnType("bit").HasDefaultValue(false);
            entity.Property(x => x.RowVersion).HasColumnName("ROW_VERSION").HasColumnType("bigint").HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(x => x.ProjectDate).HasDatabaseName("IX_PVM_PV_PROJECT_DATE");
            entity.HasIndex(x => x.ProjectType).HasDatabaseName("IX_PVM_PV_PROJECT_TYPE");
            entity.HasIndex(x => x.Developer).HasDatabaseName("IX_PVM_PV_DEVELOPER");
            entity.HasIndex(x => x.ProgramName).HasDatabaseName("IX_PVM_PV_PROGRAM_NAME");
            entity.HasIndex(x => x.ReleaseTime).HasDatabaseName("IX_PVM_PV_RELEASE_TIME");
            entity.HasIndex(x => x.IsDeleted).HasDatabaseName("IX_PVM_PV_IS_DELETED");
            entity.HasIndex(x => new { x.ProgramGroup, x.PersonnelGroup, x.ProgramName, x.ProgramVersionNumber })
                .HasDatabaseName("UX_PVM_PV_ACTIVE_UQ")
                .IsUnique()
                .HasFilter("[IS_DELETED] = 0");
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<OperationLog>(entity =>
        {
            entity.ToTable("PVM_OPERATION_LOG", table =>
            {
                table.HasCheckConstraint("CK_PVM_LOG_OP_TYPE", "[OPERATION_TYPE] IN ('CREATE', 'UPDATE', 'DELETE', 'RESTORE')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("ID").HasMaxLength(36);
            entity.Property(x => x.BusinessId).HasColumnName("BUSINESS_ID").HasMaxLength(36).IsRequired();
            entity.Property(x => x.OperationType).HasColumnName("OPERATION_TYPE").HasMaxLength(20).IsRequired();
            entity.Property(x => x.Operator).HasColumnName("OPERATOR_USERNAME").HasMaxLength(100).IsRequired();
            entity.Property(x => x.OperationTime).HasColumnName("OPERATION_TIME").HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(x => x.ProgramGroup).HasColumnName("PROGRAM_GROUP").HasMaxLength(100);
            entity.Property(x => x.ProgramName).HasColumnName("PROGRAM_NAME").HasMaxLength(200);
            entity.Property(x => x.ProgramVersion).HasColumnName("PROGRAM_VERSION").HasMaxLength(50);
            entity.Property(x => x.OperationDetail).HasColumnName("OPERATION_DETAIL").HasMaxLength(2000);
            entity.Property(x => x.BeforeData).HasColumnName("BEFORE_DATA").HasColumnType("nvarchar(max)");
            entity.Property(x => x.AfterData).HasColumnName("AFTER_DATA").HasColumnType("nvarchar(max)");
            entity.Property(x => x.IpAddress).HasColumnName("IP_ADDRESS").HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasColumnName("USER_AGENT").HasMaxLength(1000);
            entity.HasIndex(x => x.OperationTime).HasDatabaseName("IX_PVM_LOG_TIME");
            entity.HasIndex(x => x.BusinessId).HasDatabaseName("IX_PVM_LOG_BUSINESS_ID");
        });

        modelBuilder.Entity<ProjectOption>(entity =>
        {
            entity.ToTable("PVM_PROJECT_OPTION", table =>
            {
                table.HasCheckConstraint("CK_PVM_OPTION_TYPE", "[OPTION_TYPE] IN ('PROJECT_TYPE', 'STAFF_GROUP', 'PERSONNEL_GROUP', 'DEVELOPER', 'PROGRAM_GROUP', 'PROGRAM_NAME', 'COMPILER')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("ID").HasMaxLength(36);
            entity.Property(x => x.OptionType).HasColumnName("OPTION_TYPE").HasMaxLength(30).IsRequired();
            entity.Property(x => x.OptionValue).HasColumnName("OPTION_VALUE").HasMaxLength(200).IsRequired();
            entity.Property(x => x.StaffGroup).HasColumnName("STAFF_GROUP").HasMaxLength(100);
            entity.Property(x => x.PersonnelGroup).HasColumnName("PERSONNEL_GROUP").HasMaxLength(100);
            entity.Property(x => x.ProgramGroup).HasColumnName("PROGRAM_GROUP").HasMaxLength(100);
            entity.Property(x => x.SortOrder).HasColumnName("SORT_ORDER").HasDefaultValue(0);
            entity.Property(x => x.CreatedBy).HasColumnName("CREATED_BY").HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(x => x.UpdatedBy).HasColumnName("UPDATED_BY").HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("UPDATED_AT").HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(x => x.RowVersion).HasColumnName("ROW_VERSION").HasColumnType("bigint").HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(x => new { x.OptionType, x.OptionValue, x.StaffGroup, x.PersonnelGroup, x.ProgramGroup }).IsUnique();
            entity.HasIndex(x => new { x.OptionType, x.ProgramGroup }).HasDatabaseName("IX_PVM_OPTION_GROUP");
            entity.HasIndex(x => new { x.OptionType, x.StaffGroup }).HasDatabaseName("IX_PVM_OPTION_STAFF_GROUP");
            entity.HasIndex(x => new { x.OptionType, x.PersonnelGroup }).HasDatabaseName("IX_PVM_OPTION_PERSONNEL_GROUP");
        });

        modelBuilder.Entity<PvmUserPermission>(entity =>
        {
            entity.ToTable("PVM_USER_PERMISSION", table =>
            {
                table.HasCheckConstraint("CK_PVM_UP_PERMISSION", "[PERMISSION_CODE] IN ('VERSION_VIEW', 'VERSION_CREATE', 'VERSION_UPDATE', 'VERSION_DELETE', 'VERSION_IMPORT', 'LOG_VIEW', 'OPTION_MANAGE', 'PROGRAM_RELATION_MANAGE', 'PERSONNEL_MANAGE')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("ID").HasMaxLength(36);
            entity.Property(x => x.UserId).HasColumnName("USER_ID").HasMaxLength(36).IsRequired();
            entity.Property(x => x.PermissionCode).HasColumnName("PERMISSION_CODE").HasMaxLength(50).IsRequired();
            entity.Property(x => x.CreatedBy).HasColumnName("CREATED_BY").HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("CREATED_AT").HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
            entity.HasOne<PvmUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PVM_UP_USER");
            entity.HasIndex(x => new { x.UserId, x.PermissionCode }).IsUnique();
        });
    }
}
