namespace GastosApp.IntegrationTests.Support;

/// <summary>
/// Gera um CPF sintético com dígito verificador válido (mesmo algoritmo
/// de <c>GastosApp.Domain.Users.Cpf.IsValid</c>), único por execução —
/// evita colidir com o <c>CpfPointer</c> (unicidade de CPF, FEAT-26) de
/// uma execução anterior que não tenha sido limpa corretamente.
/// </summary>
public static class CpfGenerator
{
    public static string GenerateUnique()
    {
        // 9 dígitos base pseudo-aleatórios (não precisam ser
        // criptograficamente fortes — só evitar colisão entre execuções
        // concorrentes da suíte).
        var random = Random.Shared;
        var digits = new int[11];
        for (var i = 0; i < 9; i++)
            digits[i] = random.Next(0, 10);

        // Nunca gera sequência de dígitos repetidos (Cpf.IsValid rejeita).
        if (digits.Take(9).Distinct().Count() == 1)
            digits[0] = (digits[0] + 1) % 10;

        digits[9] = CalculateCheckDigit(digits, 9);
        digits[10] = CalculateCheckDigit(digits, 10);

        return string.Concat(digits);
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
