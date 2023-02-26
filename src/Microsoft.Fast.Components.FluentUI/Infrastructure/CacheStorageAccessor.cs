using Microsoft.Fast.Components.FluentUI.Utilities;
using Microsoft.JSInterop;

namespace Microsoft.Fast.Components.FluentUI.Infrastructure
{
    public class CacheStorageAccessor : JSModule
    {
        public CacheStorageAccessor(IJSRuntime js)
            : base(js, "./_content/Microsoft.Fast.Components.FluentUI/js/CacheStorageAccessor.js")
        {
        }

        public async ValueTask PutAsync(HttpRequestMessage requestMessage, HttpResponseMessage responseMessage)
        {
            string requestMethod = requestMessage.Method.Method;
            string requestBody = await GetRequestBodyAsync(requestMessage);
            string responseBody = await responseMessage.Content.ReadAsStringAsync();

            await InvokeVoidAsync("put", requestMessage.GetRequestUri(), requestMethod, requestBody, responseBody);
        }

        public async ValueTask<string> PutAndGetAsync(HttpRequestMessage requestMessage, HttpResponseMessage responseMessage)
        {
            string requestMethod = requestMessage.Method.Method;
            string requestBody = await GetRequestBodyAsync(requestMessage);
            string responseBody = await responseMessage.Content.ReadAsStringAsync();

            await InvokeVoidAsync("put", requestMessage.GetRequestUri(), requestMethod, requestBody, responseBody);

            return responseBody;
        }

        public async ValueTask<string> GetAsync(HttpRequestMessage requestMessage)
        {
            string requestMethod = requestMessage.Method.Method;
            string requestBody = await GetRequestBodyAsync(requestMessage);
            string result = await InvokeAsync<string>("get", requestMessage.GetRequestUri(), requestMethod, requestBody);

            return result;
        }

        public async ValueTask RemoveAsync(HttpRequestMessage requestMessage)
        {
            string requestMethod = requestMessage.Method.Method;
            string requestBody = await GetRequestBodyAsync(requestMessage);

            await InvokeVoidAsync("remove", requestMessage.GetRequestUri(), requestMethod, requestBody);
        }

        public async ValueTask RemoveAllAsync()
        {
            await InvokeVoidAsync("removeAll");
        }
        private static async ValueTask<string> GetRequestBodyAsync(HttpRequestMessage requestMessage)
        {
            string requestBody = string.Empty;
            if (requestMessage.Content is not null)
            {
                requestBody = await requestMessage.Content.ReadAsStringAsync();
            }

            return requestBody;
        }
    }

    public static class CacheStorageAccessorExtensions
    {
        public static string GetRequestUri(this HttpRequestMessage msg)
        {
            _ = msg is null || msg.RequestUri is null ? throw new ArgumentNullException(nameof(msg)) : true;

            return msg.RequestUri.IsAbsoluteUri ? msg.RequestUri!.PathAndQuery : $"{msg.RequestUri}";
        }
    }


}
