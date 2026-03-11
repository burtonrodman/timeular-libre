namespace Timeular.Core;

public interface IConfigProvider
{
    Task<TimeularConfig> GetConfigAsync(CancellationToken cancellationToken = default);
}
