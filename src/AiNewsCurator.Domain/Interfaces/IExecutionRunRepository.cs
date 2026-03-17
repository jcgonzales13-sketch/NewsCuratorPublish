using AiNewsCurator.Domain.Entities;

namespace AiNewsCurator.Domain.Interfaces;

public interface IExecutionRunRepository
{
    Task<long> InsertAsync(ExecutionRun run, CancellationToken cancellationToken);
    Task UpdateAsync(ExecutionRun run, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExecutionRun>> GetRecentAsync(int maxItems, CancellationToken cancellationToken);
}
