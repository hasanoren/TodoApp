using FluentValidation.TestHelper;
using TodoApp.Application.DTOs;
using TodoApp.Application.Validators;
using Xunit;

namespace TodoApp.Application.Tests;

public class PaginationTests
{
    private readonly PaginatedRequestValidator _validator = new();

    // --- PaginatedResponse Tests ---

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(5, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(100, 20, 5)]
    [InlineData(101, 20, 6)]
    public void PaginatedResponse_CalculatesTotalPagesCorrectly(int totalCount, int pageSize, int expectedTotalPages)
    {
        var response = new PaginatedResponse<string>(new List<string>(), totalCount, 1, pageSize);
        Assert.Equal(expectedTotalPages, response.TotalPages);
    }

    [Fact]
    public void PaginatedResponse_CalculatesHasPreviousPageAndHasNextPageCorrectly()
    {
        // First page of 3 pages
        var firstPage = new PaginatedResponse<int>(new List<int>(), 30, page: 1, pageSize: 10);
        Assert.False(firstPage.HasPreviousPage);
        Assert.True(firstPage.HasNextPage);

        // Middle page of 3 pages
        var middlePage = new PaginatedResponse<int>(new List<int>(), 30, page: 2, pageSize: 10);
        Assert.True(middlePage.HasPreviousPage);
        Assert.True(middlePage.HasNextPage);

        // Last page of 3 pages
        var lastPage = new PaginatedResponse<int>(new List<int>(), 30, page: 3, pageSize: 10);
        Assert.True(lastPage.HasPreviousPage);
        Assert.False(lastPage.HasNextPage);

        // Single page
        var singlePage = new PaginatedResponse<int>(new List<int>(), 5, page: 1, pageSize: 10);
        Assert.False(singlePage.HasPreviousPage);
        Assert.False(singlePage.HasNextPage);
    }

    // --- PaginatedRequest Defaults ---

    [Fact]
    public void PaginatedRequest_DefaultValues_ArePage1AndPageSize20()
    {
        var request = new PaginatedRequest();
        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
    }

    // --- PaginatedRequestValidator Tests ---

    [Fact]
    public void Validator_WhenValidRequest_ShouldNotHaveErrors()
    {
        var request = new PaginatedRequest { Page = 1, PageSize = 20 };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validator_WhenMaxPageSize_ShouldBeValid()
    {
        var request = new PaginatedRequest { Page = 5, PageSize = 100 };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Validator_WhenPageLessThanOne_ShouldHaveError(int invalidPage)
    {
        var request = new PaginatedRequest { Page = invalidPage, PageSize = 20 };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(200)]
    public void Validator_WhenPageSizeOutOfRange_ShouldHaveError(int invalidPageSize)
    {
        var request = new PaginatedRequest { Page = 1, PageSize = invalidPageSize };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }
}
