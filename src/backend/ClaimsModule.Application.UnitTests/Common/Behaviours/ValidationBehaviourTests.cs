using ClaimsModule.Application.Common.Behaviours;
using ClaimsModule.Application.Common.Models;
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

    public class PingWithWarnings : IHasValidationWarnings
    {
        public List<string> Warnings { get; } = [];
    }

    public record PingReturningWarnings(string Message) : IRequest<PingWithWarnings>;

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

    [Fact]
    public async Task Handle_with_only_warning_severity_failures_calls_next_and_attaches_warnings()
    {
        var validator = Substitute.For<IValidator<PingReturningWarnings>>();
        validator
            .ValidateAsync(Arg.Any<ValidationContext<PingReturningWarnings>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([
                new ValidationFailure("PolicyId", "No policy linked.") { Severity = Severity.Warning }
            ]));

        var sut = new ValidationBehaviour<PingReturningWarnings, PingWithWarnings>([validator]);
        var nextCalled = false;
        RequestHandlerDelegate<PingWithWarnings> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new PingWithWarnings());
        };

        var result = await sut.Handle(new PingReturningWarnings("hello"), next, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.Equal(["No policy linked."], result.Warnings);
    }

    [Fact]
    public async Task Handle_with_a_mix_of_error_and_warning_failures_throws_only_for_the_error()
    {
        var validator = Substitute.For<IValidator<PingReturningWarnings>>();
        validator
            .ValidateAsync(Arg.Any<ValidationContext<PingReturningWarnings>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([
                new ValidationFailure("PolicyId", "No policy linked.") { Severity = Severity.Warning },
                new ValidationFailure("Message", "Message is required.") { Severity = Severity.Error }
            ]));

        var sut = new ValidationBehaviour<PingReturningWarnings, PingWithWarnings>([validator]);
        RequestHandlerDelegate<PingWithWarnings> next = () => Task.FromResult(new PingWithWarnings());

        await Assert.ThrowsAsync<ValidationException>(
            () => sut.Handle(new PingReturningWarnings(""), next, CancellationToken.None));
    }
}
