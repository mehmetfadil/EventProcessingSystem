using EventProcessing.Core.Interfaces;
using EventProcessing.Core.Models;
using EventProcessing.IngestionApi.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace EventProcessing.IngestionApi.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly IEventPublisher _eventPublisher;
        private readonly IValidator<TransactionEvent> _eventValidator;

        public EventsController(
            IEventPublisher eventPublisher,
            IValidator<TransactionEvent> eventValidator)
        {
            _eventPublisher = eventPublisher;
            _eventValidator = eventValidator;
        }

        [HttpPost("batch")]
        public async Task<IActionResult> PublishBatch([FromBody] List<TransactionEvent> events, CancellationToken cancellationToken)
        {
            if (events == null || !events.Any())
            {
                return BadRequest("Event listesi boş olamaz.");
            }

            if (events.Count > 1000)
            {
                return BadRequest("Bir istekte en fazla 1.000 event gönderilebilir.");
            }

            foreach (var evt in events)
            {
                var validationResult = await _eventValidator.ValidateAsync(evt, cancellationToken);
                if (!validationResult.IsValid)
                {
                    return BadRequest(validationResult.Errors);
                }
            }

            var batchId = Guid.NewGuid();

            try
            {
                await _eventPublisher.PublishBatchAsync(events, cancellationToken);

                var response = new BatchResponse
                {
                    BatchId = batchId,
                    AcceptedCount = events.Count
                };

               
                return Accepted(response);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Mesaj broker'ına şu anda erişilemiyor.");
            }
        }
    }
}