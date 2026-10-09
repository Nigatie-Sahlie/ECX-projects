using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DaycareApi.Tests.Helpers;

public sealed class ApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;

    public async Task LoginAs(string email, string password)
    {
        var response = await Http.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.Token);
    }

    public async Task<JsonDocument> GetJson(string path)
    {
        var response = await Http.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    public async Task<HttpResponseMessage> Post(string path, object body) =>
        await Http.PostAsJsonAsync(path, body);

    public async Task<HttpResponseMessage> Put(string path, object body) =>
        await Http.PutAsJsonAsync(path, body);

    private sealed record LoginResponse(string Token);
}
