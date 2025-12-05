using Shared.DTOs;

namespace Infrastructure.Database;

public interface IPositionRepository
{
    Task UpsertPositionsAsync(IEnumerable<Position> positions, CancellationToken cancellationToken = default);
}
