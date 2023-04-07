using System.Dynamic;
using System.Net.Http.Json;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Collections;

namespace RestClient;

public class RestClient
{
    public JsonSerializerOptions JsonSerializerOptions { get; set; }

    private readonly HttpClient _httpClient;

    public RestClient()
    {
        _httpClient = new HttpClient();
    }


    public Action<HttpRequestMessage> LogRequests { get; set; }

    public Action<HttpRequestMessage, HttpResponseMessage> LogResponses { get; set; }

    public Action<HttpRequestMessage, HttpResponseMessage, Exception> LogErrors { get; set; }

    public TResponse? Invoke<TResponse>(IReturn<TResponse> requestDto)
    {
        var httpRequest = BuildMessage(requestDto);

        LogRequests?.Invoke(httpRequest);

        if (httpRequest.Content is not null)
        {
            var task = httpRequest.Content.ReadAsStringAsync();
            task.Wait();
        }

        return GetResponse<TResponse>(httpRequest);
    }


    private TResponse GetResponse<TResponse>(HttpRequestMessage httpRequest)
    {
        var httpResponse = _httpClient.Send(httpRequest);

        if (httpResponse.IsSuccessStatusCode)
        {
            var json = httpResponse.Content.ReadAsStringAsync().Result;

            LogResponses?.Invoke(httpRequest, httpResponse);

            try
            {
                if (typeof(TResponse) == typeof(HttpStatusCode))
                {
                    return (TResponse)(object)httpResponse.StatusCode;
                }
                else
                {
                    var dto = JsonSerializer.Deserialize<TResponse>(json, JsonSerializerOptions);
                    return dto;
                }
            }
            catch (Exception ex)
            {
                LogErrors?.Invoke(httpRequest, httpResponse, ex);
                return default(TResponse);
            }
        }
        else
        {
            var response = httpResponse.Content.ReadAsStringAsync().Result;
            return default;
        }
    }

    internal HttpRequestMessage BuildMessage<T>(T requestDto)
    {
        var type = requestDto?.GetType()!;
        var properties = type.GetProperties().ToList();
        var route = type.GetCustomAttribute<RouteAttribute>();

        // Route validation
        if (route is null)
            throw new NullReferenceException($"You must specify a {nameof(RouteAttribute)} for request type {type.FullName}");

        if (route.Uri is null)
            throw new NullReferenceException($"You must specify {nameof(RouteAttribute.Uri)} for request type {type.FullName} on attribute {nameof(RouteAttribute)}");

        var httpMethod = route.Verb switch
        {
            Verb.Get => HttpMethod.Get,
            Verb.Post => HttpMethod.Post,
            Verb.Put => HttpMethod.Put,
            Verb.Delete => HttpMethod.Delete,

            _ => throw new NullReferenceException($"You must specify {nameof(RouteAttribute.Verb)} for request type {type.FullName} on attribute {nameof(RouteAttribute)}")
        };

        var httpRequestMessage = new HttpRequestMessage(httpMethod, string.Empty);

        var uri = route.Uri + "?";
        var expandoObject = new ExpandoObject();

        foreach (var property in properties)
        {
            // Handle in line query parameters
            var match = Regex.Match(uri, $"{{{property.Name}}}");
            if (match.Success)
            {
                var propertyValue = property.GetValue(requestDto);

                if (propertyValue is null)
                    throw new NullReferenceException($"{property.Name} is a required field for endpoint {route.Uri}");

                uri = uri.Replace($"{{{property.Name}}}", GetUriParameterValue(propertyValue));
            }
            // Handle request body
            else if (property.GetCustomAttribute<BodyAttribute>() is BodyAttribute bodyParameter)
            {
                var propertyValue = property.GetValue(requestDto);

                if (propertyValue is not null)
                {
                    if (bodyParameter.WriteValueOnly)
                    {
                        if (httpRequestMessage.Content is not null)
                            throw new ApplicationException($"You may only specificy one property with {nameof(BodyAttribute)}.{nameof(BodyAttribute.WriteValueOnly)} on type {type.FullName} set to {true}");

                        httpRequestMessage.Content = JsonContent.Create(propertyValue, options: JsonSerializerOptions);
                    }
                    else
                    {
                        var uriParameterName = this.JsonSerializerOptions.PropertyNamingPolicy.ConvertName(bodyParameter.Alias ?? property.Name);
                        var uriParameterValue = GetUriParameterValue(propertyValue);

                        // Disable the below warning as no combination seems to correct this error even though this will not be null here
                        // Argument cannot be used for parameter due to differences in the nullability of reference types.
                        expandoObject?.TryAdd(uriParameterName, propertyValue);
                    }
                }
            }
            // Handle appended query parameters
            else
            {
                var uriParameterName = JsonSerializerOptions.PropertyNamingPolicy.ConvertName(property.Name);
                var propertyValue = property.GetValue(requestDto);

                if (propertyValue is not null)
                {
                    var uriParameterValue = GetUriParameterValue(propertyValue);
                    uri += $"{uriParameterName}={uriParameterValue}&";
                }
            }
        }

        if (httpRequestMessage.Content is null && expandoObject.Count() > 0)
        {
            httpRequestMessage.Content = JsonContent.Create(expandoObject, options: JsonSerializerOptions);
        }

        httpRequestMessage.RequestUri = new Uri(uri.TrimEnd('&', '?'));

        return httpRequestMessage;
    }

    internal string GetUriParameterValue(object value)
    {
        if (value is IConvertible convertible)
        {
            return convertible?.ToString() ?? throw new NullReferenceException();
        }
        else if (value is IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            List<string> strings = new();

            while (enumerator.MoveNext())
            {
                strings.Add(GetUriParameterValue(enumerator.Current));
            }

            return string.Join(',', strings);
        }

        throw new ApplicationException($"Value {value} is not supported in uri query strings.");
    }
}
