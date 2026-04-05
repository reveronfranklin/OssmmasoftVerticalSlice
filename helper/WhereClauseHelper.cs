using System.Text.RegularExpressions;

namespace OssmmasoftVerticalSlice.Helpers;

public static class WhereClauseHelper
{
    public static bool TryBuildCleanWhere(string? inputWhere, out string cleanWhere, out string? errorMessage)
    {
        cleanWhere = inputWhere?.Trim() ?? "";
        errorMessage = null;

        // Quitar comillas externas accidentales (ej: "'STATUS = ''A'''")
        if (cleanWhere.StartsWith("'") && cleanWhere.EndsWith("'") && cleanWhere.Length > 2)
        {
            cleanWhere = cleanWhere.Substring(1, cleanWhere.Length - 2);
        }

        // Si llega "WHERE ...", remover "WHERE" porque el SP ya arma su WHERE base.
        if (cleanWhere.StartsWith("WHERE ", StringComparison.OrdinalIgnoreCase))
        {
            cleanWhere = cleanWhere.Substring(6).Trim();
        }
        else if (cleanWhere.Equals("WHERE", StringComparison.OrdinalIgnoreCase))
        {
            cleanWhere = "";
        }

        // Validación de comillas balanceadas.
        int quoteCount = cleanWhere.Count(f => f == '\'');
        if (quoteCount % 2 != 0)
        {
            errorMessage = $"Error de sintaxis: Hay una comilla simple sin cerrar en el filtro: [{cleanWhere}]";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(cleanWhere))
        {
            // Validación de paréntesis balanceados.
            int parenthesisBalance = 0;
            foreach (char ch in cleanWhere)
            {
                if (ch == '(') parenthesisBalance++;
                if (ch == ')') parenthesisBalance--;
                if (parenthesisBalance < 0) break;
            }

            if (parenthesisBalance != 0)
            {
                errorMessage = $"Error de sintaxis: paréntesis desbalanceados en el filtro: [{cleanWhere}]";
                return false;
            }

            // Operadores mal ubicados o expresión truncada.
            if (Regex.IsMatch(cleanWhere, @"^\s*(AND|OR)\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(cleanWhere, @"\b(AND|OR)\s*$", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(cleanWhere, @"(=|<>|>=|<=|>|<|LIKE|IN|BETWEEN)\s*$", RegexOptions.IgnoreCase))
            {
                errorMessage = $"Error de sintaxis: expresión incompleta en el filtro: [{cleanWhere}]";
                return false;
            }
        }

        return true;
    }
}
