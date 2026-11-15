using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using FluentValidation;
using CoreValidationException = BankingSystemCore.CrossCuttingConcerns.Exceptions.Types.ValidationException;
using MediatR;

namespace Application.Pipelines.Validation;

/// <summary>
/// MediatR pipeline behavior that runs all FluentValidation validators of a request
/// before the handler. Throws the core ValidationException (errors grouped by property) if any rule fails.
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            var results = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            List<ValidationExceptionModel> errors = results
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
                .Select(g => new ValidationExceptionModel { Property = g.Key, Errors = g.Distinct().ToArray() })
                .ToList();

            if (errors.Count != 0)
                throw new CoreValidationException(errors);
        }

        return await next();
    }
}
