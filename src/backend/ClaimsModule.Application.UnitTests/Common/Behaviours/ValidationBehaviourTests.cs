using ClaimsModule.Application.Common.Behaviours;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using NSubstitute;
using Xunit;
using ValidationException = ClaimsModule.Application.Common.Exceptions.ValidationException;

namespace ClaimsModule.Application.UnitTests.Common.Behaviours;

public class ValidationBehaviourTests
{
    public record Ping(string Message) : IRequest<string>;

    [Fact]
    public async Task Handle_with_a_failing_validator_throws_before_calling_next()
    {
        var validator = Substitute.For<IValidator<Ping>>();
        validator
            .ValidateAsync(Arg.Any<ValidationContext<Ping>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure("Message", "Message is required.")]));

        var sut = new ValidationBehaviour<Ping, string>([validator]);
        var nextCalled = false;
        RequestHandlerDelegate<string> next = () => { nextCalled = true; return Task.FromResult("handled"); };

        await Assert.ThrowsAsync<ValidationException>(() => sut.Handle(new Ping(""), next, CancellationToken.None));

        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Handle_with_no_failures_calls_next_and_returns_its_result()
    {
        var validator = Substitute.For<IValidator<Ping>>();
        validator
            .ValidateAsync(Arg.Any<ValidationContext<Ping>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var sut = new ValidationBehaviour<Ping, string>([validator]);
        RequestHandlerDelegate<string> next = () => Task.FromResult("handled");

        var result = await sut.Handle(new Ping("hello"), next, CancellationToken.None);

        Assert.Equal("handled", result);
    }
}
