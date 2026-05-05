using System.Threading.Tasks;

namespace SmoothOperator.Application.Interfaces
{
    public interface IAuditService
    {
        Task WriteAsync(string action, string resourceType, string? resourceId = null,
            object? details = null, string outcome = "success");
    }
}
