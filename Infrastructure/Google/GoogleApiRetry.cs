namespace GoogleIntegrationService.Infrastructure.Google
{
    /// <summary>
    /// Wrapper for Google API calls: on failure, retries the request
    /// after a pause (2 seconds by default).
    /// </summary>
    public static class GoogleApiRetry
    {
        public const int DefaultMaxAttempts = 3;
        public const int DefaultDelaySeconds = 2;

        public static async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken = default,
            int maxAttempts = DefaultMaxAttempts,
            int delaySeconds = DefaultDelaySeconds)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await action(cancellationToken);
                }
                catch when (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
                {
                    // Request failed — wait and try again.
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
            }
        }
    }
}
