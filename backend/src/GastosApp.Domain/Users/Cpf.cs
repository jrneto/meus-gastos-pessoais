namespace GastosApp.Domain.Users;

/// <summary>
/// Validação de CPF por dígito verificador (algoritmo oficial) — usada pelo
/// RegisterUserCommandValidator. Puramente algorítmica, sem I/O; a checagem
/// de unicidade entre usuários é responsabilidade do IUserProfileRepository
/// (Infrastructure), não deste tipo.
/// </summary>
public static class Cpf
{
    public static bool IsValid(string digits)
    {
        if (digits.Length != 11 || !digits.All(char.IsDigit))
            return false;

        // Sequências com todos os dígitos iguais "fecham" o cálculo do dígito
        // verificador mas nunca são CPFs reais — regra padrão de validação no Brasil.
        if (digits.Distinct().Count() == 1)
            return false;

        var numbers = digits.Select(c => c - '0').ToArray();

        return numbers[9] == CalculateCheckDigit(numbers, 9)
            && numbers[10] == CalculateCheckDigit(numbers, 10);
    }

    private static int CalculateCheckDigit(int[] numbers, int length)
    {
        var sum = 0;
        for (var i = 0; i < length; i++)
            sum += numbers[i] * (length + 1 - i);

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
