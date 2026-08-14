using MediatR;

namespace SDPP.BuildingBlocks.Application;

/// <summary>A write use case that mutates state and returns a Result.</summary>
public interface ICommand : IRequest<Result>;

/// <summary>A write use case that mutates state and returns a Result of T.</summary>
public interface ICommand<T> : IRequest<Result<T>>;

/// <summary>A read-only use case. Query handlers must never write to the database.</summary>
public interface IQuery<T> : IRequest<Result<T>>;
