using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Fast.Components.FluentUI.Infrastructure;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

namespace Microsoft.Fast.Components.FluentUI;

public class HttpBasedStaticAssetService : IStaticAssetService
{
    private const string FLUENT_ICON_API = "FluentIconAPI";
    private const string FLUENT_ICON_API_SCOPE = "FluentIconAPIScope";
    private const string FLUENT_ICON_API_PATH = @"/FluentIcon?folderPath=";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NavigationManager? _navigationManager;
    private readonly IConfiguration? _configuration;
    private readonly ITokenAcquisition? _tokenAcquisition;
    private readonly MicrosoftIdentityConsentAndConditionalAccessHandler? _handler;
    private readonly CacheStorageAccessor _cacheStorageAccessor;

    public HttpBasedStaticAssetService(
        IHttpClientFactory httpClientFactory,
        CacheStorageAccessor cacheStorageAccessor,
        NavigationManager? navigationManager = null,
        IConfiguration? configuration = null,
        ITokenAcquisition? tokenAcquisition = null,
        MicrosoftIdentityConsentAndConditionalAccessHandler? handler = null)
    {
        _httpClientFactory = httpClientFactory;
        _navigationManager = navigationManager;
        _configuration = configuration;
        _tokenAcquisition = tokenAcquisition;
        _handler = handler;
        _cacheStorageAccessor = cacheStorageAccessor;
    }

    public async Task<string?> GetAsync(string assetUrl, bool useCache = true)
    {
        string? result = null;

        HttpRequestMessage message = CreateMessage(assetUrl);
        _ = message is null ? throw new ArgumentNullException(nameof(HttpRequestMessage)) : true;

        if (useCache)
        {
            // Get the result from the cache
            result = await _cacheStorageAccessor.GetAsync(message);
            if (!string.IsNullOrWhiteSpace(result)) { return result; }
        }

        //If not in the cache (or cache not used), download the asset
        HttpResponseMessage? response = default;
        try
        {
            using (var httpClient = _httpClientFactory.CreateClient())
            {
                _ = httpClient is null ? throw new ApplicationException("Error getting httpclient") : true;

                httpClient.BaseAddress ??= new Uri(_navigationManager?.BaseUri ?? "http://localhost");

                response = httpClient.Send(message);

                _ = response is null ? throw new ApplicationException("Error getting icon path") : true;

                response.EnsureSuccessStatusCode();
            }

            //For Azure AD clients, call FluentIcon API with Bearer Auth Token to authenticate call
            if (response.RequestMessage?.RequestUri?.Host == "login.microsoftonline.com")//if (response.IsSuccessStatusCode)
            {
                using (var httpClient = _httpClientFactory.CreateClient())
                {
                    var fluentIconApi = _configuration?[FLUENT_ICON_API];
                    _ = fluentIconApi is null ? throw new NullReferenceException($"'{HttpBasedStaticAssetService.FLUENT_ICON_API}' config value undefined") : true;

                    httpClient.BaseAddress = new Uri(fluentIconApi);

                    var accessToken = await GetAccessToken();
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    message = CreateMessage(assetUrl, true);

                    response = httpClient!.Send(message);

                    response.EnsureSuccessStatusCode();
                }
            }

            // Store the response in the cache and get the result
            if (useCache)
            {
                result = await _cacheStorageAccessor.PutAndGetAsync(message, response);
            }
            else
            {
                result = await response.Content.ReadAsStringAsync();
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine(ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            throw;
        }
    }

    private HttpRequestMessage CreateMessage(string url, bool callApi = false)
    {
        var msg = callApi switch
        {
            true => new HttpRequestMessage(HttpMethod.Get, $"{FLUENT_ICON_API_PATH}{url}"),
            _ => new HttpRequestMessage(HttpMethod.Get, url)
        };

        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/svg+xml"));
        return msg;
    }

    public async Task<string> GetAccessToken()
    {
        var fluentIconApiScope = _configuration?[FLUENT_ICON_API_SCOPE];
        _ = fluentIconApiScope is null ? throw new NullReferenceException($"'{HttpBasedStaticAssetService.FLUENT_ICON_API_SCOPE}' config value undefined") : true;

        string[] scopes = { fluentIconApiScope };

        // we use MSAL.NET to get a token to call the API On Behalf Of the current user
        try
        {
            string accessToken = await _tokenAcquisition!.GetAccessTokenForUserAsync(scopes);
            return accessToken;
        }
        catch (MicrosoftIdentityWebChallengeUserException ex)
        {
            _tokenAcquisition!.ReplyForbiddenWithWwwAuthenticateHeader(scopes, ex.MsalUiRequiredException);
            throw;
        }
        catch (MsalUiRequiredException ex)
        {
            _tokenAcquisition!.ReplyForbiddenWithWwwAuthenticateHeader(scopes, ex);
            throw;
        }
        catch (Exception ex)
        {
            _handler!.HandleException(ex);
            throw;
        }
    }
}