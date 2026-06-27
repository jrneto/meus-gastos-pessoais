namespace GastosApp.Application.Common.Exceptions;

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException(string message = "Email ou senha inválidos") : base(message) { }
}
