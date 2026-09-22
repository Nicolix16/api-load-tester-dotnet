using System.Diagnostics;
using System.Net.Http;
using System.Text;

namespace ApiLoadTester;

internal static class Program
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

    private static async Task Main()
    {
        Console.WriteLine("==================================================");
        Console.WriteLine("   PRUEBAS DE CARGA CONTROLADA - WEB API .NET");
        Console.WriteLine("==================================================\n");

        var url = ReadUrl("http://localhost:5132/api/auth/login");
        var username = ReadText("Usuario/Email de prueba", "wolfang");
        var password = ReadText("Contraseña", "Pass123!");
        var rounds = ReadPositiveInt("Número de rondas", 3);
        var results = new List<RoundResult>();

        for (var round = 1; round <= rounds; round++)
        {
            Console.WriteLine($"\n--- Configuración de la ronda {round} de {rounds} ---");
            var totalRequests = ReadPositiveInt("Número total de peticiones", 100);
            var maxConcurrency = ReadPositiveInt("Concurrencia / solicitudes simultáneas", 5);
            Console.WriteLine("Ejecutando prueba de carga...");

            var result = await RunRoundAsync(url, username, password, totalRequests, maxConcurrency);
            results.Add(result);
            PrintResult(round, result);
        }

        PrintComparison(results);
    }

    private static async Task<RoundResult> RunRoundAsync(
        string url, string username, string password, int totalRequests, int maxConcurrency)
    {
        var successCount = 0;
        var failCount = 0;
        long totalTicks = 0;
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = new Task[totalRequests];
        var jsonPayload = $"{{\"usernameOrEmail\":\"{username}\",\"password\":\"{password}\"}}";
        var globalTimer = Stopwatch.StartNew();

        for (var i = 0; i < totalRequests; i++)
        {
            await semaphore.WaitAsync();
            tasks[i] = SendRequestAsync();
        }

        await Task.WhenAll(tasks);
        globalTimer.Stop();

        var totalMs = (double)totalTicks / Stopwatch.Frequency * 1000;
        var averageLatency = totalRequests > 0 ? totalMs / totalRequests : 0;

        return new RoundResult(totalRequests, maxConcurrency, successCount, failCount,
            globalTimer.ElapsedMilliseconds, totalMs, averageLatency);

        async Task SendRequestAsync()
        {
            var requestTimer = Stopwatch.StartNew();
            try
            {
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                using var response = await Client.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                    Interlocked.Increment(ref successCount);
                else
                    Interlocked.Increment(ref failCount);
            }
            catch (HttpRequestException)
            {
                Interlocked.Increment(ref failCount);
            }
            catch (TaskCanceledException)
            {
                Interlocked.Increment(ref failCount);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref failCount);
            }
            finally
            {
                requestTimer.Stop();
                Interlocked.Add(ref totalTicks, requestTimer.ElapsedTicks);
                semaphore.Release();
            }
        }
    }

    private static void PrintResult(int round, RoundResult result)
    {
        var successRate = (double)result.SuccessCount / result.TotalRequests * 100;
        Console.WriteLine($"\n================ RESULTADOS RONDA {round} ================");
        Console.WriteLine($"Peticiones totales : {result.TotalRequests}");
        Console.WriteLine($"Concurrencia       : {result.MaxConcurrency}");
        Console.WriteLine($"Exitosas           : {result.SuccessCount}");
        Console.WriteLine($"Fallidas           : {result.FailCount}");
        Console.WriteLine($"Tasa de éxito      : {successRate:F2}%");
        Console.WriteLine($"Tiempo total       : {result.TotalMilliseconds} ms");
        Console.WriteLine($"Latencia promedio  : {result.AverageLatencyMilliseconds:F2} ms");
        Console.WriteLine("========================================================");
    }

    private static void PrintComparison(IReadOnlyList<RoundResult> results)
    {
        Console.WriteLine("\n================ COMPARACIÓN DE RONDAS ================");
        Console.WriteLine("Ronda | Peticiones | Concurrencia | Éxito | Latencia media | Tiempo total");

        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            var successRate = (double)result.SuccessCount / result.TotalRequests * 100;
            Console.WriteLine($"{index + 1,5} | {result.TotalRequests,10} | {result.MaxConcurrency,12} | " +
                $"{successRate,5:F2}% | {result.AverageLatencyMilliseconds,14:F2} ms | " +
                $"{result.TotalMilliseconds,11} ms");
        }

        Console.WriteLine("========================================================");
    }

    private static string ReadText(string label, string defaultValue)
    {
        Console.Write($"{label} [Predeterminado: {defaultValue}]: ");
        var value = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static string ReadUrl(string defaultValue)
    {
        while (true)
        {
            var value = ReadText("URL del endpoint", defaultValue);

            if (Uri.TryCreate(value, UriKind.Absolute, out var endpoint) &&
                (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps))
            {
                return endpoint.AbsoluteUri;
            }

            Console.WriteLine("La URL debe ser absoluta y comenzar con http:// o https://.");
        }
    }

    private static int ReadPositiveInt(string label, int defaultValue)
    {
        while (true)
        {
            Console.Write($"{label} [Predeterminado: {defaultValue}]: ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return defaultValue;

            if (int.TryParse(input, out var value) && value > 0)
                return value;

            Console.WriteLine("Introduce un entero positivo.");
        }
    }

    private sealed record RoundResult(
        int TotalRequests, int MaxConcurrency, int SuccessCount, int FailCount,
        long TotalMilliseconds, double TotalRequestMilliseconds, double AverageLatencyMilliseconds);
}