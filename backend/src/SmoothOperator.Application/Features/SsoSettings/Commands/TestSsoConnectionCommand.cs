using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Interfaces.Sso;

namespace SmoothOperator.Application.Features.SsoSettings.Commands
{
    public sealed record TestSsoConnectionCommand : IRequest<SsoTestResult>;

    public sealed class TestSsoConnectionCommandHandler : IRequestHandler<TestSsoConnectionCommand, SsoTestResult>
    {
        private readonly ISsoConnectionTester _tester;
        private readonly IAuditService _audit;

        public TestSsoConnectionCommandHandler(ISsoConnectionTester tester, IAuditService audit)
        {
            _tester = tester;
            _audit = audit;
        }

        public async Task<SsoTestResult> Handle(TestSsoConnectionCommand request, CancellationToken cancellationToken)
        {
            var result = await _tester.TestAsync();
            await _audit.WriteAsync("sso.tested", "sso", "",
                new { success = result.Success, message = result.Message });
            return result;
        }
    }
}
