using RestClient.Naming;
using System.Text.Json;

namespace RestClient;

public static class NamingPolicies
{
    public static JsonNamingPolicy SnakePascal => new SnakePascalJsonNamingPolicy();
}
