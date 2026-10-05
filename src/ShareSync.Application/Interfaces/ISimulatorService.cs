using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.Simulator;

namespace ShareSync.Application.Interfaces;

public interface ISimulatorService
{
    Task<ApiResponse<SimulationResultDto>> SimulateTransactionAsync(
        SimulateTransactionRequestDto request,
        int userId,
        CancellationToken cancellationToken = default);
}
