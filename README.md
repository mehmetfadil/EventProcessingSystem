# Event Processing System

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![xUnit](https://img.shields.io/badge/xUnit-2.5.3-blue)](https://xunit.net/)
[![NUnit](https://img.shields.io/badge/NUnit-4.1.0-green)](https://nunit.org/)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

Finansal işlem event'lerini kabul eden, RabbitMQ üzerinden asenkron işleyen ve müşteri/gün/para birimi bazında özetleyen bir **Clean Architecture** mikroservis sistemidir.

---

## Sistemi çalıştırma ve test etme adımları

### Ön Gereksinimler

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Altyapıyı başlatma

```bash
docker compose up --build
```

Bu komut şu konteynerları ayağa kaldırır:

| Konteyner | Port |
|---|---|
| `rabbitmq` (RabbitMQ 3 + Management UI) | `5672`, `15672` |
| `sqlserver` (SQL Server 2022) | `1433` |
| `ingestion-api` (Ingestion API) | `5084` |
| `aggregation-service` (Aggregation Service) | `5094` |

### Testleri çalıştırma

```bash
# Birim testleri (Docker ile, .NET SDK gerektirmez)
docker compose --profile test run --rm unit-tests

# Entegrasyon testleri (Docker ile, altyapı otomatik başlar)
docker compose --profile test run --rm integration-tests
```

> `.NET SDK` kurulu ise aşağıdaki komutlar da kullanılabilir:

```bash
# Birim testleri (InMemory DB, Docker gerektirmez)
dotnet test EventProcessing.UnitTests/EventProcessing.UnitTests.csproj

# Entegrasyon testleri (Docker konteynerları çalışıyor olmalı)
dotnet test EventProcessing.IntegrationTests/EventProcessing.IntegrationTests.csproj
```

### Yük testi

```bash
dotnet run --project EventProcessing.LoadGenerator
```

### Manuel test

```powershell
# Event gönder
irm -Method POST -Uri http://localhost:5084/api/v1/events/batch `
  -ContentType "application/json" `
  -Body '[{"eventId":"a1b2c3d4-e5f6-7890-abcd-ef1234567890","customerId":"test-001","type":0,"amount":150.75,"currency":"TRY","occurredAt":"2026-08-08T10:00:00Z"}]'

# Özet sorgula
irm "http://localhost:5094/api/v1/customers/test-001/daily-summary?date=2026-08-08&currency=TRY"
```

---

## Mimari ve servis sorumlulukları

```
┌──────────────────┐     ┌──────────────┐     ┌─────────────────────┐
│   LoadGenerator   │────▶│ IngestionApi  │────▶│      RabbitMQ       │
│  (Console App)    │     │  (Web API)    │     │  (Message Broker)   │
└──────────────────┘     └──────────────┘     └─────────┬───────────┘
                                                        │
                                              ┌─────────▼───────────┐
                                              │ AggregationService   │
                                              │  (Background Worker) │
                                              └─────────┬───────────┘
                                                        │
                                              ┌─────────▼───────────┐
                                              │     SQL Server       │
                                              │  (EventProcessingDb) │
                                              └─────────────────────┘
```

| Katman / Servis | Sorumluluk |
|---|---|
| **EventProcessing.Core** | `TransactionEvent`, `DailySummary`, `ProcessedEvent` modelleri; `TransactionType` enum'ı; `IEventPublisher` ve `ISummaryRepository` arayüzleri |
| **EventProcessing.Infrastructure** | EF Core `AppDbContext`, `SummaryRepository` (transaction + idempotency), `EventPublisher` (RabbitMQ bağlantısı), migration'lar |
| **IngestionApi** | `POST /api/v1/events/batch` — gelen event'leri `FluentValidation` ile doğrular, `IEventPublisher` üzerinden RabbitMQ'ya basar |
| **AggregationService** | `EventConsumer` (BackgroundService) — RabbitMQ'dan event'leri okur, duplicate kontrolü yapar, `DailySummary` tablosuna yazar. `GET /api/v1/customers/{id}/daily-summary` — özet sorgulama |
| **LoadGenerator** | Yük testi için 100.000 rastgele event üretir, batch'ler halinde IngestionApi'ye gönderir |

---

## Veri ve mesaj akışı

1. `LoadGenerator` rastgele `TransactionEvent`'ler üretir (100 müşteri, 3 para birimi, %10 duplicate)
2. `IngestionApi` gelen JSON'u deserialize eder, `TransactionEventValidator` ile doğrular
3. Geçerli event'ler `EventPublisher` tarafından RabbitMQ `transaction_events` kuyruğuna basılır (`persistent: true`)
4. `AggregationService` içindeki `EventConsumer` (BackgroundService) kuyruktan event'leri okur (`prefetchCount: 50`)
5. Her event `SummaryRepository.ProcessEventTransactionallyAsync` ile işlenir:
   - `ProcessedEvents` tablosunda idempotency kontrolü
   - `DailySummaries` tablosunda müşteri/gün/para birimi bazında toplulaştırma
6. Kullanıcı `GET /api/v1/customers/{customerId}/daily-summary` ile anlık özeti sorgulayabilir

---

## Idempotency ve transaction yaklaşımı

- **Idempotency:** Her event işlenmeden önce `ProcessedEvents` tablosunda `EventId` kontrolü yapılır. Daha önce işlenmişse atlanır, aynı event tekrar işlenmez.
- **Transaction:** `ReadCommitted` izolasyon seviyesinde ADO.NET transaction'ı kullanılır. `ProcessedEvents` kaydı ve `DailySummaries` güncellemesi aynı transaction içinde atomik olarak commit edilir.
- **Eşzamanlılık:** Aynı müşteri/gün/para birimine aynı anda iki event gelirse, ikinci işlem `DbUpdateException` alır ve transaction rollback yapılır. Consumer katmanında bu hata 3 kere retry edilir.

---

## Retry, DLQ ve backpressure yaklaşımı

### Retry

`EventConsumer` içinde veritabanı işlemi başarısız olursa **3 kere** retry yapılır, her denemede bekleme süresi artar (1sn → 2sn → 3sn).

### Dead Letter Queue (DLQ)

3 retry sonunda hâlâ başarısız olan mesajlar `transaction_events_dlq` kuyruğuna yönlendirilir (`requeue: false`). DLQ'da biriken mesajlar manuel inceleme veya yeniden işleme için saklanır.

### Backpressure

`BasicQos(prefetchCount: 50)` ile consumer'ın aynı anda en fazla 50 mesaj alması sağlanır. Bu, consumer'ın aşırı yüklenmesini engeller.

---

## Yük testi sonucu ve gözlemler

| Metrik | Değer |
|---|---|
| Toplam event | 100.000 |
| Benzersiz (unique) | 90.000 |
| Tekrar eden (duplicate) | 10.000 (%10) |
| Müşteri çeşitliliği | 100 |
| Batch boyutu | 500 |
| Batch sayısı | 200 |
| **Kabul edilen event** | **100.000** |
| **Hatalı batch** | **0** |
| **Ortalama hız** | **~14.800 event/sn** |
| Toplam süre | ~6.8 saniye |

**Gözlemler:** Sistem 100.000 event'i hatasız işledi. Darboğaz IngestionApi değil, AggregationService'in veritabanı yazma hızıdır (tek consumer, senkron DB yazımı). Consumer sayısı artırılarak throughput yükseltilebilir.

### Birim test sonuçları (24 test)

| Senaryo | Sonuç |
|---|---|
| Geçerli event (Credit) doğrulamadan geçer | ✅ |
| Geçerli event (Debit) doğrulamadan geçer | ✅ |
| Boş EventId reddedilir | ✅ |
| Boş CustomerId reddedilir | ✅ |
| 101 karakter CustomerId reddedilir | ✅ |
| 100 karakter CustomerId kabul edilir | ✅ |
| Sıfır Amount reddedilir | ✅ |
| Negatif Amount reddedilir | ✅ |
| Boş Currency reddedilir | ✅ |
| 2 karakter Currency reddedilir | ✅ |
| 4 karakter Currency reddedilir | ✅ |
| Küçük harf Currency reddedilir | ✅ |
| `DateTime.MinValue` OccurredAt reddedilir | ✅ |
| Yeni Credit event'i DailySummary oluşturur | ✅ |
| Yeni Debit event'i DailySummary oluşturur | ✅ |
| Aynı event tekrar gönderilirse duplicate atlanır | ✅ |
| Birden fazla event aynı müşteri/gün/para biriminde doğru toplanır | ✅ |
| Farklı müşterilerin özetleri birbirinden bağımsızdır | ✅ |
| Aynı müşteri farklı günler ayrı özetlenir | ✅ |
| Var olmayan özet sorgusu `null` döner | ✅ |
| İşlenen event `ProcessedEvents` tablosuna eklenir | ✅ |
| NUnit — geçerli event doğrulaması | ✅ |
| NUnit — boş EventId reddedilir | ✅ |
| NUnit — sanity check | ✅ |

### Entegrasyon test sonuçları (7 test — Docker SQL Server'a karşı)

| Senaryo | Sonuç |
|---|---|
| Yeni event DailySummary oluşturur | ✅ |
| Aynı event iki kere gönderilirse duplicate atlanır | ✅ |
| Credit + Debit aynı gün doğru toplanır | ✅ |
| Var olmayan özet `null` döner | ✅ |
| Farklı para birimleri ayrı özetlenir | ✅ |
| İşlenen event ProcessedEvents tablosuna eklenir | ✅ |
| NUnit — sanity check | ✅ |

---

## Bilinen eksikler ve daha fazla sürede yapılacaklar

- Consumer horizontal scaling (birden fazla consumer instance'ı) ve competing consumers pattern'i
- Health check endpoint'leri (`/health`, `/ready`)
- Prometheus / Grafana ile metrik toplama ve dashboard
- CI/CD pipeline (GitHub Actions) ile otomatik test ve build
- API rate limiting
- Integration test kapsamının artırılması (RabbitMQ publish/consume uçtan uca testi)

---

## Önemli teknik kararlar ve trade-off'lar

| Karar | Gerekçe | Trade-off |
|---|---|---|
| **MSSQL** yerine PostgreSQL kullanılmadı | SQL Server Management Studio (SSMS) aşinalığı ve yerel geliştirme kolaylığı | PostgreSQL'e göre daha ağır konteyner imajı ve lisans maliyeti |
| **Tek consumer** | Geliştirme süresi kısıtı | Yüksek throughput senaryolarında darboğaz |
| **RabbitMQ yerine Kafka kullanılmadı** | RabbitMQ'nun düşük gecikmeli mesajlaşma ve DLQ desteği | Kafka'nın daha iyi olduğu event streaming ve replay senaryoları kapsam dışı |
| **InMemory DB (testlerde)** | Birim testlerinin hızlı ve Docker'dan bağımsız çalışması | Transaction davranışı birebir aynı değil (uyarı bastırıldı) |
| **Serilog** | PDF zorunlu isteri — yapılandırılmış loglama, konsol + günlük dosya çıktısı | `appsettings.json` üzerinden konfigürasyon yapılamaz, kod tarafında `LoggerConfiguration` gerekir |

---

## Kullanılan AI veya kod üretim araçları ve hangi amaçlarla kullanıldıkları

| Araç | Kullanım Amacı |
|---|---|
| **Claude Code** | Birim testlerin ve entegrasyon testlerinin yazılması, bu README dosyasının oluşturulması, `EventPublisher` ve `LoadGenerator`'daki QueueDeclare çakışması ile enum eşleşme hatalarının tespit edilmesi |

Mimari kararlar, servis tasarımı ve iş mantığı tamamen manuel olarak geliştirilmiştir. AI araçları yalnızca test yazımı ve dokümantasyon için yardımcı olarak kullanılmıştır.
