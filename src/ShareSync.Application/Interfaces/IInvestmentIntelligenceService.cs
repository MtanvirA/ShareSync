using System;
using System.Threading;
using System.Threading.Tasks;
using ShareSync.Application.Common.Models;
using ShareSync.Application.DTOs.InvestmentIntelligence;

namespace ShareSync.Application.Interfaces;

public interface IInvestmentIntelligenceService
{
    Task<ApiResponse<HistoricalInvestmentProfileDto>> GetHistoricalInvestmentProfileAsync(
        int companyId, 
        DateTime? fromDate = null, 
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);
}
