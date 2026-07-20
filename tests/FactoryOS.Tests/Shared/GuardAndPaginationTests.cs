using FactoryOS.Shared.Guards;
using FactoryOS.Shared.Pagination;

namespace FactoryOS.Tests.Shared;

public sealed class GuardTests
{
    [Fact]
    public void AgainstNull_throws_for_null_and_returns_the_value_otherwise()
    {
        Assert.Throws<ArgumentNullException>(() => Guard.AgainstNull<string>(null));
        Assert.Equal("x", Guard.AgainstNull("x"));
    }

    [Fact]
    public void AgainstNullOrWhiteSpace_rejects_blank_strings()
    {
        Assert.Throws<ArgumentException>(() => Guard.AgainstNullOrWhiteSpace("  "));
        Assert.Equal("ok", Guard.AgainstNullOrWhiteSpace("ok"));
    }

    [Fact]
    public void AgainstNegative_rejects_negative_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.AgainstNegative(-1m));
        Assert.Equal(0m, Guard.AgainstNegative(0m));
    }

    [Fact]
    public void AgainstOutOfRange_enforces_inclusive_bounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.AgainstOutOfRange(11, 1, 10));
        Assert.Equal(10, Guard.AgainstOutOfRange(10, 1, 10));
    }
}

public sealed class PaginationTests
{
    [Fact]
    public void PageRequest_normalizes_the_page_number_and_clamps_the_size()
    {
        var request = PageRequest.Of(pageNumber: 0, pageSize: 10_000);

        Assert.Equal(1, request.PageNumber);
        Assert.Equal(PageRequest.MaxPageSize, request.PageSize);
    }

    [Fact]
    public void Skip_and_take_follow_from_the_page()
    {
        var request = PageRequest.Of(pageNumber: 3, pageSize: 20);

        Assert.Equal(40, request.Skip);
        Assert.Equal(20, request.Take);
    }

    [Fact]
    public void Metadata_computes_total_pages_and_navigation_flags()
    {
        var meta = PaginationMetadata.From(PageRequest.Of(pageNumber: 2, pageSize: 10), totalCount: 25);

        Assert.Equal(3, meta.TotalPages);
        Assert.True(meta.HasPrevious);
        Assert.True(meta.HasNext);
    }

    [Fact]
    public void A_paged_result_carries_its_items_and_metadata()
    {
        int[] items = [1, 2, 3];
        var page = PagedResult.Create<int>(items, PageRequest.Of(pageNumber: 1, pageSize: 3), totalCount: 9);

        Assert.Equal(3, page.Items.Count);
        Assert.Equal(3, page.Metadata.TotalPages);
        Assert.False(page.Metadata.HasPrevious);
    }

    [Fact]
    public void An_empty_page_reports_no_items()
    {
        var page = PagedResult.Empty<int>(PageRequest.Of());

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Metadata.TotalCount);
    }
}
