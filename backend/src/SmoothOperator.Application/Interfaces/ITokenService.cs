using System;
using SmoothOperator.Domain.Models;

namespace SmoothOperator.Application.Interfaces
{
    public interface ITokenService
    {
        string CreateToken(User user);
        TimeSpan TokenLifetime { get; }
    }
}
