namespace GastosApp.Application.Common.Exceptions;

public class EmailAlreadyExistsException : Exception
{
    public EmailAlreadyExistsException(string message = "Email já cadastrado") : base(message) { }
}
