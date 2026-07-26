using TapeTracker.Models;

namespace TapeTracker.Services;

public interface IDashboardService
{
    Task<DashboardStats> GetStatsAsync();
}
