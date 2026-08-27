using FluentAssertions;
using GastosApp.Application.Auth.Queries.GetCurrentUser;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Domain.Users;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class GetCurrentUserQueryHandlerTests
{
    private readonly IUserProfileRepository _userProfileRepositoryMock;
    private readonly GetCurrentUserQueryHandler _handler;

    public GetCurrentUserQueryHandlerTests()
    {
        _userProfileRepositoryMock = Substitute.For<IUserProfileRepository>();
        _handler = new GetCurrentUserQueryHandler(_userProfileRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnProfileFields_WhenProfileExists()
    {
        // Arrange
        var profile = UserProfile.Restore("user-123", "Fulano da Silva", "11999998888", "11144477735", DateTimeOffset.UtcNow);
        _userProfileRepositoryMock.FindByUserIdAsync("user-123", Arg.Any<CancellationToken>())
            .Returns(profile);

        var query = new GetCurrentUserQuery("user-123", "neto@email.com");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be("user-123");
        result.Value.Email.Should().Be("neto@email.com");
        result.Value.Name.Should().Be("Fulano da Silva");
        result.Value.PhoneNumber.Should().Be("11999998888");
        result.Value.Cpf.Should().Be("11144477735");
    }

    [Fact]
    public async Task Handle_ShouldReturnNullFields_WhenProfileDoesNotExist()
    {
        // Arrange
        _userProfileRepositoryMock.FindByUserIdAsync("user-123", Arg.Any<CancellationToken>())
            .Returns((UserProfile?)null);

        var query = new GetCurrentUserQuery("user-123", "neto@email.com");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().BeNull();
        result.Value.PhoneNumber.Should().BeNull();
        result.Value.Cpf.Should().BeNull();
    }
}
