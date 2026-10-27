using MediatR;

namespace Reviews.Application.Common.Interfaces;

/// <summary>Команда — змінює стан і нічого не повертає (у MediatR результатом є Unit).</summary>
public interface ICommand : IRequest;

/// <summary>Команда, що повертає результат (наприклад, створений відгук).</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;
