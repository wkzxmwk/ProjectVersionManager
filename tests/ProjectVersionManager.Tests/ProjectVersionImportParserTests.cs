using ClosedXML.Excel;
using ProjectVersionManager.Api.Infrastructure;
using ProjectVersionManager.Api.Services;

namespace ProjectVersionManager.Tests;

public sealed class ProjectVersionImportParserTests
{
    [Fact]
    public void Parse_AcceptsTemplateColumnsAndChineseStatus()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("项目版本导入");
        WriteHeaders(sheet);
        sheet.Cell("A2").Value = new DateTime(2026, 8, 1);
        sheet.Cell("B2").Value = "SR";
        sheet.Cell("C2").Value = "张三";
        sheet.Cell("D2").Value = "MES";
        sheet.Cell("F2").Value = "Demo.exe";
        sheet.Cell("G2").Value = "1.0.0";
        sheet.Cell("I2").Value = "李四";
        sheet.Cell("J2").Value = new DateTime(2026, 8, 1, 10, 30, 0);
        sheet.Cell("P2").Value = "提前释放";

        var rows = ProjectVersionImportParser.Parse(sheet);

        var row = Assert.Single(rows);
        Assert.Equal(new DateTime(2026, 8, 1), row.ProjectDate);
        Assert.Equal("MES", row.ProgramGroup);
        Assert.Equal("1.0.0", row.ProgramVersion);
        Assert.Equal("PRE_RELEASED", row.Status);
        Assert.Equal(new DateTime(2026, 8, 1, 10, 30, 0), row.CompileTime);
    }

    [Fact]
    public void Parse_RejectsInvalidRowsWithoutReturningPartialData()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("项目版本导入");
        WriteHeaders(sheet);
        sheet.Cell("A2").Value = "not-a-date";
        sheet.Cell("B2").Value = "SR";
        sheet.Cell("C2").Value = "张三";
        sheet.Cell("D2").Value = "MES";
        sheet.Cell("F2").Value = "Demo.exe";
        sheet.Cell("G2").Value = "1.0.0";
        sheet.Cell("P2").Value = "未知状态";

        var exception = Assert.Throws<RequestValidationException>(
            () => ProjectVersionImportParser.Parse(sheet));

        Assert.Contains("项目时间", exception.Message);
        Assert.Contains("状态无效", exception.Message);
    }

    [Fact]
    public void Parse_AcceptsCompiledStatus()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("项目版本导入");
        WriteHeaders(sheet);
        sheet.Cell("A2").Value = new DateTime(2026, 8, 2);
        sheet.Cell("B2").Value = "SR";
        sheet.Cell("C2").Value = "张三";
        sheet.Cell("D2").Value = "MES";
        sheet.Cell("F2").Value = "Demo.exe";
        sheet.Cell("G2").Value = "1.0.1";
        sheet.Cell("P2").Value = "已编译";

        var row = Assert.Single(ProjectVersionImportParser.Parse(sheet));

        Assert.Equal("COMPILED", row.Status);
    }

    [Fact]
    public void Parse_AcceptsCanceledStatus()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("项目版本导入");
        WriteHeaders(sheet);
        sheet.Cell("A2").Value = new DateTime(2026, 9, 8);
        sheet.Cell("B2").Value = "SR";
        sheet.Cell("C2").Value = "张三";
        sheet.Cell("D2").Value = "MES";
        sheet.Cell("F2").Value = "Demo.exe";
        sheet.Cell("G2").Value = "1.0.2";
        sheet.Cell("P2").Value = "已取消";

        var row = Assert.Single(ProjectVersionImportParser.Parse(sheet));

        Assert.Equal("CANCELED", row.Status);
    }

    [Fact]
    public void Parse_RequiresUnchangedTemplateHeaders()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("项目版本导入");
        WriteHeaders(sheet);
        sheet.Cell("A1").Value = "日期";

        var exception = Assert.Throws<RequestValidationException>(
            () => ProjectVersionImportParser.Parse(sheet));

        Assert.Contains("请使用系统提供的导入模板", exception.Message);
    }

    private static void WriteHeaders(IXLWorksheet sheet)
    {
        for (var index = 0; index < ProjectVersionImportParser.Headers.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = ProjectVersionImportParser.Headers[index];
        }
    }
}
