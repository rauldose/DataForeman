using MediatR;
using DataForeman.Shared.DTOs;

namespace DataForeman.Api.Features.Flows;

// Queries
public record GetFlowsQuery : IRequest<GetFlowsResult>;
public record GetFlowsResult(IReadOnlyList<FlowDto> Flows);

public record GetFlowByIdQuery(Guid Id) : IRequest<FlowDto?>;

// Commands
public record CreateFlowCommand(
    string Name,
    string? Description,
    Guid? FolderId,
    bool? Shared,
    string? ExecutionMode,
    int? ScanRateMs,
    bool? LogsEnabled,
    int? LogsRetentionDays,
    string? Definition
) : IRequest<FlowDto>;

public record UpdateFlowCommand(
    Guid Id,
    string? Name,
    string? Description,
    Guid? FolderId,
    bool? Shared,
    bool? Deployed,
    bool? TestMode,
    string? ExecutionMode,
    int? ScanRateMs,
    bool? LogsEnabled,
    int? LogsRetentionDays,
    string? Definition
) : IRequest<FlowDto?>;

public record DeleteFlowCommand(Guid Id) : IRequest<bool>;

public record DeployFlowCommand(Guid Id, bool Deployed) : IRequest<DeployFlowResult?>;
public record DeployFlowResult(bool Deployed);
