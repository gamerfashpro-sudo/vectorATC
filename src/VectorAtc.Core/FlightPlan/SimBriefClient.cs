using System.Net;

namespace VectorAtc.Core.FlightPlan;

public interface ISimBriefClient
{
    /// <summary>Запрашивает последний план пользователя. Ввод: ID пилота (цифры) или имя пользователя.</summary>
    Task<FlightPlanResult> FetchLatestAsync(string idOrUsername, CancellationToken cancellationToken = default);
}

/// <summary>
/// Запрос последнего плана через публичный адрес SimBrief (xml.fetcher.php, json=v2). По описанию Navigraph ключ API для этого запроса
/// не упомянут, а приложение должно назвать себя параметром client. Условия использования разработчиками SimBrief здесь не прочитаны.
/// Сетевое обращение происходит только по действию пользователя (кнопка или сохранённый ID при запуске).
/// </summary>
public sealed class HttpSimBriefClient : ISimBriefClient
{
    public const string ClientName = "VectorATC";
    private const string Endpoint = "https://www.simbrief.com/api/xml.fetcher.php";

    private readonly HttpClient _http;

    public HttpSimBriefClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    /// <summary>Адрес запроса: цифры считаются ID пилота (userid), остальное именем пользователя (username).</summary>
    public static string BuildUrl(string idOrUsername)
    {
        var value = idOrUsername.Trim();
        var key = value.Length > 0 && value.All(char.IsAsciiDigit) ? "userid" : "username";
        return $"{Endpoint}?{key}={Uri.EscapeDataString(value)}&client={ClientName}&json=v2";
    }

    public async Task<FlightPlanResult> FetchLatestAsync(string idOrUsername, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idOrUsername))
            return FlightPlanResult.Fail(FlightPlanError.NotFound);

        try
        {
            using var response = await _http.GetAsync(BuildUrl(idOrUsername), cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.BadRequest)
                return FlightPlanResult.Fail(FlightPlanError.NotFound);
            if (!response.IsSuccessStatusCode)
                return FlightPlanResult.Fail(FlightPlanError.Network);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return SimBriefParser.Parse(body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;
            return FlightPlanResult.Fail(FlightPlanError.Network);
        }
    }
}
