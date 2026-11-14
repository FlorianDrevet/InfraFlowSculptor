using InfraFlowSculptor.Application.GitConnections;

namespace InfraFlowSculptor.Application.Tests.GitConnections;

public sealed class SetGitConnectionRepositoryAccessCommandValidatorTests
{
    [Fact]
    public void EmptyProjectListClosesRepositoryAccess()
    {
        var validator = new SetGitConnectionRepositoryAccessCommandValidator();
        var command = new SetGitConnectionRepositoryAccessCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            2,
            "contoso/shop",
            false,
            []);

        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
