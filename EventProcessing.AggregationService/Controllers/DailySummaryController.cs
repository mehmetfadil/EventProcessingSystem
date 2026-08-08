using EventProcessing.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EventProcessing.AggregationService.Controllers
{
    [ApiController]
    [Route("api/v1/customers")]
    public class DailySummaryController : ControllerBase
    {
        private readonly ISummaryRepository _summaryRepository;

        public DailySummaryController(ISummaryRepository summaryRepository)
        {
            _summaryRepository = summaryRepository;
        }

        [HttpGet("{customerId}/daily-summary")]
        public async Task<IActionResult> GetDailySummary(
            [FromRoute] string customerId,
            [FromQuery] string date,
            [FromQuery] string currency,
            CancellationToken cancellationToken)
        {
            // 1. Parametre Doğrulamaları (400 Bad Request durumları)
            if (string.IsNullOrWhiteSpace(customerId))
                return BadRequest("Müşteri ID gereklidir.");

            if (!DateTime.TryParse(date, out DateTime parsedDate))
                return BadRequest("Geçersiz tarih formatı. Lütfen YYYY-MM-DD formatında gönderin.");

            if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
                return BadRequest("Geçersiz para birimi. Üç harfli bir kod olmalıdır (Örn: TRY).");

            // 2. Veritabanından Özeti Sorgula
            var summary = await _summaryRepository.GetSummaryAsync(customerId, parsedDate.Date, currency.ToUpper(), cancellationToken);

            // 3. Kayıt Bulunamazsa (404 Not Found)
            if (summary == null)
            {
                return NotFound(new { Message = "Belirtilen kriterlere uygun özet bulunamadı." });
            }

            // 4. Başarılı Sonuç (200 OK)
            return Ok(summary);
        }
    }
}