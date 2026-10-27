namespace ProjectVersionManager.Api.Dtos;

public sealed record ApiResponse<T>(bool Success, string Message, T? Data)
{
    public static ApiResponse<T> Ok(T data, string message = "操作成功") => new(true, message, data);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => (int)Math.Ceiling(Total / (double)PageSize);
}

public sealed record ProgramGroupOptionDto(string OptionValue, string PersonnelGroup, string StaffGroup);
