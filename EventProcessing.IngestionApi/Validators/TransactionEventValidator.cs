using EventProcessing.Core.Models;
using FluentValidation;

namespace EventProcessing.IngestionApi.Validators
{
    public class TransactionEventValidator : AbstractValidator<TransactionEvent>
    {
        public TransactionEventValidator()
        {
            RuleFor(x => x.EventId)
                .NotEmpty().WithMessage("EventId boş olamaz.");

            RuleFor(x => x.CustomerId)
                .NotEmpty().WithMessage("CustomerId boş olamaz.")
                .MaximumLength(100).WithMessage("CustomerId en fazla 100 karakter olabilir.");

            RuleFor(x => x.Amount)
                .GreaterThan(0).WithMessage("Amount (tutar) sıfırdan büyük olmalıdır.");

            RuleFor(x => x.Currency)
                .NotEmpty()
                .Length(3).WithMessage("Currency üç harfli para birimi kodu olmalıdır.")
                .Must(c => c == c.ToUpper()).WithMessage("Currency büyük harflerden oluşmalıdır.");

            RuleFor(x => x.OccurredAt)
                .NotEmpty().WithMessage("OccurredAt zaman bilgisi boş olamaz.");
        }
    }
}