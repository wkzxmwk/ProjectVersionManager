namespace ProjectVersionManager.Api.Infrastructure;

public static class SystemPermissions
{
    public const string ClaimType = "permission";

    public const string VersionView = "VERSION_VIEW";
    public const string VersionCreate = "VERSION_CREATE";
    public const string VersionUpdate = "VERSION_UPDATE";
    public const string VersionDelete = "VERSION_DELETE";
    public const string VersionImport = "VERSION_IMPORT";
    public const string LogView = "LOG_VIEW";
    public const string OptionManage = "OPTION_MANAGE";
    public const string ProgramRelationManage = "PROGRAM_RELATION_MANAGE";
    public const string PersonnelManage = "PERSONNEL_MANAGE";

    public static readonly IReadOnlyList<string> All =
    [
        VersionView,
        VersionCreate,
        VersionUpdate,
        VersionDelete,
        VersionImport,
        LogView,
        OptionManage,
        ProgramRelationManage,
        PersonnelManage
    ];
}
