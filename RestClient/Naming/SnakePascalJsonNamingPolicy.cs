using System.Text.Json;
using System.Text.RegularExpressions;

namespace RestClient.Naming;

public class SnakePascalJsonNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        if (Regex.IsMatch(name, "^[A-Z]"))
        {
            var snakeCase = Regex.Replace(name, "[A-Z]{1}", m => $"_{m.Value.ToLower()}").TrimStart('_');
            return snakeCase;
        }
        else if (Regex.IsMatch(name, "^[a-z]"))
        {
            var caseConverted = Regex.Replace(name, "(^|_)[a-z]", match => match.Value.ToUpper());
            var pascalCase = Regex.Replace(caseConverted, "_", match => string.Empty);

            return pascalCase;
        }

        return name;
    }
}