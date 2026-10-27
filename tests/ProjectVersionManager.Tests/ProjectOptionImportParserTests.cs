using ClosedXML.Excel;
using ProjectVersionManager.Api.Infrastructure;
using ProjectVersionManager.Api.Services;

namespace ProjectVersionManager.Tests;

public sealed class ProjectOptionImportParserTests
{
    [Fact]
    public void Parse_AcceptsChineseAndCodeCategoriesAndDefaultsSortOrder()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("项目选项");
        sheet.Cell("A1").Value = "选项分类";
        sheet.Cell("B1").Value = "选项内容";
        sheet.Cell("C1").Value = "排序";
        sheet.Cell("A2").Value = "项目类型";
        sheet.Cell("B2").Value = "SR";
        sheet.Cell("C2").Value = 5;
        sheet.Cell("A3").Value = "PROGRAM_NAME";
        sheet.Cell("B3").Value = "Demo.exe";

        var rows = ProjectOptionImportParser.Parse(sheet);

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal(ProjectOptionTypes.ProjectType, row.OptionType);
                Assert.Equal("SR", row.OptionValue);
                Assert.Equal(5, row.SortOrder);
            },
            row =>
            {
                Assert.Equal(ProjectOptionTypes.ProgramName, row.OptionType);
                Assert.Equal("Demo.exe", row.OptionValue);
                Assert.Equal(20, row.SortOrder);
            });
    }

    [Fact]
    public void Parse_RejectsInvalidHeaderWithoutReturningPartialRows()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("项目选项");
        sheet.Cell("A1").Value = "分类";
        sheet.Cell("B1").Value = "内容";
        sheet.Cell("C1").Value = "顺序";
        sheet.Cell("A2").Value = "项目类型";
        sheet.Cell("B2").Value = "SR";

        var exception = Assert.Throws<RequestValidationException>(
            () => ProjectOptionImportParser.Parse(sheet));

        Assert.Contains("第一行必须依次为", exception.Message);
    }

    [Fact]
    public void Parse_ReadsProgramNameProjectGroupFromFourthColumn()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("项目选项");
        sheet.Cell("A1").Value = "选项分类";
        sheet.Cell("B1").Value = "选项内容";
        sheet.Cell("C1").Value = "排序";
        sheet.Cell("D1").Value = "所属专案";
        sheet.Cell("A2").Value = "程序名";
        sheet.Cell("B2").Value = "frmOuterbox.frm";
        sheet.Cell("C2").Value = 10;
        sheet.Cell("D2").Value = "WHExtend";

        var row = Assert.Single(ProjectOptionImportParser.Parse(sheet));

        Assert.Equal(ProjectOptionTypes.ProgramName, row.OptionType);
        Assert.Equal("frmOuterbox.frm", row.OptionValue);
        Assert.Equal("WHExtend", row.ProgramGroup);
    }
}
