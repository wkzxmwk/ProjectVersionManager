namespace ProjectVersionManager.Api.Infrastructure;

public static class ProjectOptionTypes
{
    public const string ProjectType = "PROJECT_TYPE";
    public const string Developer = "DEVELOPER";
    public const string StaffGroup = "STAFF_GROUP";
    public const string PersonnelGroup = "PERSONNEL_GROUP";
    public const string ProgramGroup = "PROGRAM_GROUP";
    public const string ProgramName = "PROGRAM_NAME";
    public const string Compiler = "COMPILER";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [ProjectType, Developer, StaffGroup, PersonnelGroup, ProgramGroup, ProgramName, Compiler],
        StringComparer.Ordinal);
}
