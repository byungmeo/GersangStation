namespace Core;

public static class HttpClientProvider
{
    public static readonly HttpClient Http = Create();

    private static HttpClient Create()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            AutomaticDecompression = System.Net.DecompressionMethods.None,
            MaxConnectionsPerServer = 8
        };

        return new HttpClient(handler, disposeHandler: true);
    }
}
