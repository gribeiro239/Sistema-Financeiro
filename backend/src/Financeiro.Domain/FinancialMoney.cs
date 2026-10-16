namespace Financeiro.Domain;

public static class FinancialMoney
{
    public const decimal Maximum = 999_999_999_999m;
    public static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    public static decimal Positive(decimal amount)
    {
        var rounded = Round(amount);
        if (rounded <= 0 || amount > Maximum || rounded > Maximum)
            throw new ArgumentException("O valor deve ser de pelo menos um centavo e respeitar o limite permitido.");
        return rounded;
    }
}
