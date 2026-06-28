using FluentAssertions;
using PropertySearch.Domain.Enums;
using PropertySearch.Infrastructure.Stations;
using Xunit;

namespace PropertySearch.Infrastructure.Tests;

public sealed class StationModePrecedenceTests
{
    [Fact]
    public void Underground_outranks_other_modes()
    {
        var modes = new[] { TransportMode.Dlr, TransportMode.Overground, TransportMode.Underground };

        StationModePrecedence.SelectPrimaryMode(modes).Should().Be(TransportMode.Underground);
    }

    [Fact]
    public void Elizabeth_line_wins_when_no_underground()
    {
        var modes = new[] { TransportMode.Overground, TransportMode.Dlr, TransportMode.ElizabethLine };

        StationModePrecedence.SelectPrimaryMode(modes).Should().Be(TransportMode.ElizabethLine);
    }

    [Fact]
    public void A_single_mode_is_returned_unchanged()
    {
        StationModePrecedence.SelectPrimaryMode([TransportMode.Dlr]).Should().Be(TransportMode.Dlr);
    }

    [Fact]
    public void No_modes_throws()
    {
        var act = () => StationModePrecedence.SelectPrimaryMode([]);

        act.Should().Throw<ArgumentException>();
    }
}
