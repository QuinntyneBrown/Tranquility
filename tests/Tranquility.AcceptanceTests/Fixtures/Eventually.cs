using Xunit;

namespace Tranquility.AcceptanceTests.Fixtures;

/// <summary>Bounded deadline polling — never bare sleeps in tests.</summary>
public static class Eventually
{
    public static async Task Async(Func<Task<bool>> condition, string because, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail($"Condition not met within the deadline: {because}");
    }
}
