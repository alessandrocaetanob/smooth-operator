namespace SmoothOperator.Application.Exceptions
{
    public sealed class BadRequestException : AppException
    {
        public BadRequestException(string message) : base(message) { }
    }
}
