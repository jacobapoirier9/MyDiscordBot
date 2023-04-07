using CliHelper;
using Discord.Commands;
using Discord;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System;
using NLog;
using ServiceStack;
using System.Linq;

public class TriviaController : Controller
{
    private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

    private class CustomJsonNamingPolicy : JsonNamingPolicy
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

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonSerializerOptions;


    private readonly IConfiguration _configuration;
    public TriviaController(IConfiguration configuration)
    {
        _configuration = configuration;

        _httpClient = new HttpClient();
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = new CustomJsonNamingPolicy()
        };
    }

    [Cli("trivia")]
    public async Task<string> LoadTriviaQuestionAsync()
    {
        _logger.Info("Loading trivia");
        var clues = await SendHttpRequest(new GetRandomClues { Count = 1 });
        var clue = clues.First();

        var response = $"{clue.Question}|{clue.Answer}";
        return response;
    }


    private async Task<T> SendHttpRequest<T>(IReturn<T> request)
    {
        var type = request.GetType();
        var properties = type.GetProperties();
        var route = type.GetCustomAttribute<RouteAttribute>();

        var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, string.Empty);

        var uri = $"{_configuration.GetValue<string>("Trivia:Api:Uri")}{route.RelativePath}?";
        foreach (var property in properties)
        {
            var value = property.GetValue(request);

            if (value is null)
                continue;

            var uriValue = default(string);
            if (value is DateOnly dateOnly)
                uriValue = dateOnly.ToString("yyyy-MM-dd");
            else
                uriValue = value.ToString();

            var uriName = _jsonSerializerOptions.PropertyNamingPolicy.ConvertName(property.Name);

            uri += $"{uriName}={uriValue}&";
        }

        httpRequestMessage.RequestUri = new Uri(uri.TrimEnd('&', '?'));

        var response = await _httpClient.SendAsync(httpRequestMessage);

        if (response.IsSuccessStatusCode)
        {
            var json = await response.ReadToEndAsync();

            try
            {
                var dto = JsonSerializer.Deserialize<T>(json, options: _jsonSerializerOptions);
                return dto;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "An error occured parsing response as json");
                return default;
            }
        }
        else
        {
            _logger.Error("{StatusCode} {Message}", response.StatusCode, response.ReasonPhrase, response.Content.ReadAsString());
            return default;
        }
    }
}

internal class Category
{
    public int Id { get; set; }
    public string Title { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int CluesCount { get; set; }

    public List<Clue> Clues { get; set; }
}

internal class Clue
{
    public int Id { get; set; }
    public string Answer { get; set; }
    public string Question { get; set; }
    public int? Value { get; set; }
    public DateTime Airdate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int CategoryId { get; set; }
    public int GameId { get; set; }
    public object InvalidCount { get; set; }
    public Category Category { get; set; }
}

[Route("/random")]
internal class GetRandomClues : IReturn<List<Clue>>
{
    public int Count { get; set; }
}


[Route("/clues")]
internal class GetClues : IReturn<List<Clue>>
{
    /// <summary>
    /// the value of the clue in dollars
    /// </summary>
    public int? Value { get; set; }

    /// <summary>
    /// the id of the category you want to return
    /// </summary>
    public int? Category { get; set; }

    /// <summary>
    /// earliest date to show, based on original air date
    /// </summary>
    public DateOnly? MinDate { get; set; }

    /// <summary>
    /// latest date to show, based on original air date
    /// </summary>
    public DateOnly? MaxDate { get; set; }

    /// <summary>
    /// offsets the returned clues. Useful in pagination
    /// </summary>
    public int? Offset { get; set; }
}

[Route("/random")]
internal class GetRandom : IReturn<List<Clue>>
{
    /// <summary>
    /// amount of clues to return, limited to 100 at a time
    /// </summary>
    public int? Count { get; set; }
}

[Route("/final")]
internal class GetFinal : IReturn<List<Clue>>
{
    /// <summary>
    /// amount of clues to return, limited to 100 at a time
    /// </summary>
    public int? Count { get; set; }
}

[Route("/categories")]
internal class GetCategories : IReturn<List<Category>>
{
    /// <summary>
    /// amount of categories to return, limited to 100 at a time
    /// </summary>
    public int? Count { get; set; }

    /// <summary>
    /// offsets the starting id of categories returned. Useful in pagination.
    /// </summary>
    public int? Offset { get; set; }
}

[Route("/category")]
internal class GetCategory : IReturn<Category>
{
    /// <summary>
    /// Required: the ID of the category to return.
    /// </summary>
    public int? Id { get; set; }
}

internal interface IReturn<T> { }

internal class RouteAttribute : Attribute
{
    public string? RelativePath { get; private set; }

    public RouteAttribute(string? relativePath)
    {
        RelativePath = relativePath;
    }
}
