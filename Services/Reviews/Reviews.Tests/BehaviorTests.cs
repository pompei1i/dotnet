using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Reviews.Application;
using Reviews.Application.Common.Behaviors;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Dtos;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;

namespace Reviews.Tests;

public class BehaviorTests
{
    private static ServiceProvider BuildApplication(IReviewRepository repository)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TestData.CreateMapper());
        services.AddSingleton(repository);
        services.AddSingleton(Substitute.For<IDiscussionRepository>());
        services.AddApplication(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    private static CreateReviewCommand Command(int rating)
        => new(1, "C# для початківців", 7, "Олена Коваль", rating, "Текст", null, null);

    [Fact]
    public void Behaviors_run_in_registration_order()
    {
        using var provider = BuildApplication(Substitute.For<IReviewRepository>());

        var behaviors = provider.GetServices<IPipelineBehavior<CreateReviewCommand, ReviewDto>>()
            .Select(b => b.GetType().GetGenericTypeDefinition())
            .ToList();

        Assert.Equal([typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>), typeof(PerformanceBehavior<,>)], behaviors);
    }

    [Fact]
    public async Task Invalid_command_never_reaches_the_handler()
    {
        var repository = Substitute.For<IReviewRepository>();
        using var provider = BuildApplication(repository);
        var sender = provider.GetRequiredService<ISender>();

        var exception = await Assert.ThrowsAsync<ValidationException>(() => sender.Send(Command(rating: 10)));

        Assert.Contains(exception.Errors, e => e.PropertyName == "Rating");
        await repository.DidNotReceive().AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Valid_command_goes_through_the_pipeline_to_the_handler()
    {
        var repository = Substitute.For<IReviewRepository>();
        using var provider = BuildApplication(repository);

        var review = await provider.GetRequiredService<ISender>().Send(Command(rating: 4));

        Assert.Equal(4, review.Rating);
        await repository.Received(1).AddAsync(Arg.Is<Review>(r => r.Rating.Value == 4), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidationBehavior_merges_errors_of_all_validators_without_duplicates()
    {
        var first = new InlineValidator<CreateReviewCommand>();
        first.RuleFor(c => c.Rating).InclusiveBetween(1, 5);
        var second = new InlineValidator<CreateReviewCommand>();
        second.RuleFor(c => c.AuthorName).Must(_ => false).WithMessage("second validator");
        var behavior = new ValidationBehavior<CreateReviewCommand, ReviewDto>([first, second]);
        var nextCalled = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            Command(rating: 10),
            _ => { nextCalled = true; return Task.FromResult<ReviewDto>(null!); },
            CancellationToken.None));

        Assert.False(nextCalled);
        Assert.Equal(["Rating", "AuthorName"], exception.Errors.Select(e => e.PropertyName));
    }

    [Fact]
    public async Task PerformanceBehavior_warns_about_a_slow_handler()
    {
        var logger = new ListLogger<PerformanceBehavior<CreateReviewCommand, ReviewDto>>();
        var behavior = new PerformanceBehavior<CreateReviewCommand, ReviewDto>(new PerformanceSettings(10), logger);

        await behavior.Handle(Command(5), async ct => { await Task.Delay(50, ct); return null!; }, CancellationToken.None);

        var entry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.StartsWith("Slow handler CreateReviewCommand", entry.Message);
    }

    [Fact]
    public async Task LoggingBehavior_logs_failure_and_rethrows()
    {
        var logger = new ListLogger<LoggingBehavior<CreateReviewCommand, ReviewDto>>();
        var behavior = new LoggingBehavior<CreateReviewCommand, ReviewDto>(logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(Command(5), _ => throw new InvalidOperationException("boom"), CancellationToken.None));

        Assert.Contains(logger.Entries, e => e.Message.StartsWith("Handling CreateReviewCommand"));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("InvalidOperationException: boom"));
        Assert.DoesNotContain(logger.Entries, e => e.Message.StartsWith("Handled"));
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
