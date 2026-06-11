namespace GoogleIntegrationService.Infrastructure.Google
{
    /// <summary>
    /// Обгортка для викликів Google API: у разі помилки повторює запит
    /// після паузи (за замовчуванням 2 секунди).
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
                    // Помилка запиту — чекаємо й пробуємо ще раз.
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
            }
        }
    }
}
