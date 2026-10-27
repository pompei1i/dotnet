using MediatR;

namespace Reviews.Application.Common.Interfaces;

/// <summary>Запит — лише читає дані, стан не змінює.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
