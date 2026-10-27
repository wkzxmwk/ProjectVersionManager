using ProjectVersionManager.Api.Dtos;
using ProjectVersionManager.Api.Entities;

namespace ProjectVersionManager.Api.Infrastructure;

public static class MappingExtensions
{
    public static ProjectVersionDto ToDto(this ProjectVersion entity) =>
        new(
            entity.Id,
            entity.ProjectDate,
            entity.ProjectType,
            entity.Developer,
            entity.ProgramGroup,
            entity.PersonnelGroup,
            entity.ProgramName,
            entity.ProgramVersionNumber,
            entity.Compiler,
            entity.CompileTime,
            entity.CompiledProgramVersion,
            entity.VersionDescription,
            entity.ReleaseTime,
            entity.BranchName,
            entity.Remark1,
            entity.Remark2,
            entity.Status,
            entity.CreatedBy,
            entity.CreatedAt,
            entity.UpdatedBy,
            entity.UpdatedAt,
            entity.IsDeleted,
            entity.RowVersion);

    public static void Apply(this SaveProjectVersionRequest request, ProjectVersion entity)
    {
        entity.ProjectDate = request.ProjectDate.Date;
        entity.ProjectType = request.ProjectType.Trim();
        entity.Developer = request.Developer.Trim();
        entity.ProgramGroup = request.ProgramGroup.Trim();
        entity.PersonnelGroup = Clean(request.PersonnelGroup);
        entity.ProgramName = request.ProgramName.Trim();
        entity.ProgramVersionNumber = request.ProgramVersion.Trim();
        entity.Compiler = Clean(request.Compiler);
        entity.CompileTime = request.CompileTime;
        entity.CompiledProgramVersion = Clean(request.CompiledProgramVersion);
        entity.VersionDescription = Clean(request.VersionDescription);
        entity.ReleaseTime = request.ReleaseTime;
        entity.BranchName = Clean(request.BranchName);
        entity.Remark1 = Clean(request.Remark1);
        entity.Remark2 = Clean(request.Remark2);
        entity.Status = request.Status.Trim().ToUpperInvariant();
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
