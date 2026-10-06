namespace InfraFlowSculptor.Acceptance.Tests;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711", Justification = "xUnit requires this test collection definition class to end with Collection.")]
[CollectionDefinition("Jobs acceptance", DisableParallelization = true)]
public sealed class JobsTestCollection : ICollectionFixture<JobsTestFixture>
{
    public const string Name = "Jobs acceptance";
}
