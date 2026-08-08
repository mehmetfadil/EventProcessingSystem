using System.Diagnostics;
using System.Text;
using System.Text.Json;

var apiBaseUrl = args.Length > 0 ? args[0] : "http://localhost:5084";
var endpoint = $"{apiBaseUrl}/api/v1/events/batch";

int totalTargetEvents = 100000;
int duplicatePercentage = 10;
int customerCount = 100;
int batchSize = 500;

Console.WriteLine("=== Event Processing System - Load Generator ===");
Console.WriteLine($"Hedef API Endpoint : {endpoint}");
Console.WriteLine($"Toplam Event Sayısı: {totalTargetEvents}");
Console.WriteLine($"Duplicate Oranı    : %{duplicatePercentage}");
Console.WriteLine($"Müşteri Çeşitliliği: {customerCount} farklı müşteri");
Console.WriteLine($"Batch Boyutu       : {batchSize}\n");

int uniqueCount = totalTargetEvents * (100 - duplicatePercentage) / 100;
int duplicateCount = totalTargetEvents - uniqueCount;

var uniqueIds = Enumerable.Range(0, uniqueCount).Select(_ => Guid.NewGuid()).ToList();
var allEventIds = new List<Guid>(totalTargetEvents);
allEventIds.AddRange(uniqueIds);

var rand = new Random();
for (int i = 0; i < duplicateCount; i++)
{
    var randomExistingId = uniqueIds[rand.Next(uniqueIds.Count)];
    allEventIds.Add(randomExistingId);
}
allEventIds = allEventIds.OrderBy(_ => rand.Next()).ToList();

var customers = Enumerable.Range(1, customerCount).Select(c => $"customer-{c:D3}").ToList();
int[] types = { 0, 1 }; // 0: Credit, 1: Debit
string[] currencies = { "TRY", "USD", "EUR" };

var events = new List<TransactionEventDto>(totalTargetEvents);
for (int i = 0; i < totalTargetEvents; i++)
{
    events.Add(new TransactionEventDto
    {
        EventId = allEventIds[i],
        CustomerId = customers[rand.Next(customers.Count)],
        Type = types[rand.Next(types.Length)],
        Amount = Math.Round((decimal)(rand.NextDouble() * 950 + 50), 2),
        Currency = currencies[rand.Next(currencies.Length)],
        OccurredAt = DateTime.UtcNow.AddMinutes(-rand.Next(1440))
    });
}

var batches = events
    .Select((x, index) => new { Index = index, Value = x })
    .GroupBy(x => x.Index / batchSize)
    .Select(g => g.Select(x => x.Value).ToList())
    .ToList();

Console.WriteLine($"Toplam {batches.Count} adet batch oluşturuldu. Gönderim başlatılıyor...\n");

using var client = new HttpClient();
var stopwatch = Stopwatch.StartNew();

long acceptedEventCount = 0;
long errorCount = 0;
int batchIndex = 0;

foreach (var batch in batches)
{
    batchIndex++;
    try
    {
        // Doğrudan liste (Array) olarak serialize ediyoruz
        var json = JsonSerializer.Serialize(batch, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(endpoint, content);
        if (response.IsSuccessStatusCode)
        {
            Interlocked.Add(ref acceptedEventCount, batch.Count);
            if (batchIndex % 20 == 0 || batchIndex == batches.Count)
            {
                Console.WriteLine($"İlerleme: Batch {batchIndex}/{batches.Count} başarıyla gönderildi.");
            }
        }
        else
        {
            Interlocked.Increment(ref errorCount);
            var errorDetails = await response.Content.ReadAsStringAsync();

            if (errorCount <= 5)
            {
                Console.WriteLine($"[HATA] Batch {batchIndex} başarısız oldu! HTTP Durum Kodu: {(int)response.StatusCode} ({response.ReasonPhrase})");
                Console.WriteLine($"Sunucu Yanıtı: {errorDetails}\n");
            }
        }
    }
    catch (Exception ex)
    {
        Interlocked.Increment(ref errorCount);
        if (errorCount <= 5)
        {
            Console.WriteLine($"[BAĞLANTI HATASI] Batch {batchIndex}: {ex.Message}");
        }
    }
}

stopwatch.Stop();

Console.WriteLine("\n================ LOAD TEST RAPORU ================");
Console.WriteLine($"Toplam Süre           : {stopwatch.Elapsed.TotalSeconds:F2} saniye");
Console.WriteLine($"Hedeflenen Event      : {totalTargetEvents}");
Console.WriteLine($"Benzersiz (Unique)    : {uniqueCount}");
Console.WriteLine($"Tekrar Eden (Duplicate): {duplicateCount}");
Console.WriteLine($"Kabul Edilen Event    : {acceptedEventCount}");
Console.WriteLine($"Hatalı Batch Sayısı   : {errorCount}");
Console.WriteLine($"Ortalama Hız          : {totalTargetEvents / stopwatch.Elapsed.TotalSeconds:F2} event/saniye");
Console.WriteLine("====================================================");

public class TransactionEventDto
{
    public Guid EventId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public int Type { get; set; } // 1: Credit, 2: Debit
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}