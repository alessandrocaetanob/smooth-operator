using System.Threading.Tasks;

namespace SmoothOperator.Application.Interfaces.Sso
{
    public record SsoTestResult(bool Success, string Message, object? Details = null);

    public interface ISsoConnectionTester
    {
        Task<SsoTestResult> TestAsync();
    }
}
