namespace ProjectChicago.Identity.Tests.Fixtures;

internal static class TestCredentialFixture
{
    internal static string GetTestUserCredential() => Guid.NewGuid().ToString().Substring(0, 12);
}
