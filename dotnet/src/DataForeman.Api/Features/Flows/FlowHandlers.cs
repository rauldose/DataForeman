using MediatR;
using DataForeman.Api.Repositories;
using DataForeman.Shared.DTOs;
using DataForeman.Shared.Models;

namespace DataForeman.Api.Features.Flows;

public class GetFlowsQueryHandler : IRequestHandler<GetFlowsQuery, GetFlowsResult>
{
    private readonly IFlowRepository _repository;

    public GetFlowsQueryHandler(IFlowRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetFlowsResult> Handle(GetFlowsQuery request, CancellationToken cancellationToken)
    {
        var flows = await _repository.GetAllAsync(cancellationToken);
        var dtos = flows
            .OrderByDescending(f => f.UpdatedAt)
            .Select(f => new FlowDto(
                f.Id, f.Name, f.Description, f.OwnerUserId, f.FolderId,
                f.Deployed, f.Shared, f.TestMode, f.ExecutionMode, f.ScanRateMs,
                f.LogsEnabled, f.LogsRetentionDays, null, f.CreatedAt, f.UpdatedAt))
            .ToList();
        
        return new GetFlowsResult(dtos);
    }
}

public class GetFlowByIdQueryHandler : IRequestHandler<GetFlowByIdQuery, FlowDto?>
{
    private readonly IFlowRepository _repository;

    public GetFlowByIdQueryHandler(IFlowRepository repository)
    {
        _repository = repository;
    }

    public async Task<FlowDto?> Handle(GetFlowByIdQuery request, CancellationToken cancellationToken)
    {
        var flow = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (flow == null) return null;

        return new FlowDto(
            flow.Id, flow.Name, flow.Description, flow.OwnerUserId, flow.FolderId,
            flow.Deployed, flow.Shared, flow.TestMode, flow.ExecutionMode, flow.ScanRateMs,
            flow.LogsEnabled, flow.LogsRetentionDays, flow.Definition, flow.CreatedAt, flow.UpdatedAt);
    }
}

public class CreateFlowCommandHandler : IRequestHandler<CreateFlowCommand, FlowDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateFlowCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<FlowDto> Handle(CreateFlowCommand request, CancellationToken cancellationToken)
    {
        var flow = new Flow
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            FolderId = request.FolderId,
            Shared = request.Shared ?? false,
            ExecutionMode = request.ExecutionMode ?? "manual",
            ScanRateMs = request.ScanRateMs ?? 1000,
            LogsEnabled = request.LogsEnabled ?? true,
            LogsRetentionDays = request.LogsRetentionDays ?? 30,
            Definition = request.Definition ?? "{}",
            Deployed = false,
            TestMode = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Flows.AddAsync(flow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new FlowDto(
            flow.Id, flow.Name, flow.Description, flow.OwnerUserId, flow.FolderId,
            flow.Deployed, flow.Shared, flow.TestMode, flow.ExecutionMode, flow.ScanRateMs,
            flow.LogsEnabled, flow.LogsRetentionDays, flow.Definition, flow.CreatedAt, flow.UpdatedAt);
    }
}

public class UpdateFlowCommandHandler : IRequestHandler<UpdateFlowCommand, FlowDto?>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateFlowCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<FlowDto?> Handle(UpdateFlowCommand request, CancellationToken cancellationToken)
    {
        var flow = await _unitOfWork.Flows.GetByIdAsync(request.Id, cancellationToken);
        if (flow == null) return null;

        if (request.Name != null) flow.Name = request.Name;
        if (request.Description != null) flow.Description = request.Description;
        if (request.FolderId != null) flow.FolderId = request.FolderId;
        if (request.Shared != null) flow.Shared = request.Shared.Value;
        if (request.Deployed != null) flow.Deployed = request.Deployed.Value;
        if (request.TestMode != null) flow.TestMode = request.TestMode.Value;
        if (request.ExecutionMode != null) flow.ExecutionMode = request.ExecutionMode;
        if (request.ScanRateMs != null) flow.ScanRateMs = request.ScanRateMs.Value;
        if (request.LogsEnabled != null) flow.LogsEnabled = request.LogsEnabled.Value;
        if (request.LogsRetentionDays != null) flow.LogsRetentionDays = request.LogsRetentionDays.Value;
        if (request.Definition != null) flow.Definition = request.Definition;

        flow.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Flows.UpdateAsync(flow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new FlowDto(
            flow.Id, flow.Name, flow.Description, flow.OwnerUserId, flow.FolderId,
            flow.Deployed, flow.Shared, flow.TestMode, flow.ExecutionMode, flow.ScanRateMs,
            flow.LogsEnabled, flow.LogsRetentionDays, flow.Definition, flow.CreatedAt, flow.UpdatedAt);
    }
}

public class DeleteFlowCommandHandler : IRequestHandler<DeleteFlowCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteFlowCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteFlowCommand request, CancellationToken cancellationToken)
    {
        var flow = await _unitOfWork.Flows.GetByIdAsync(request.Id, cancellationToken);
        if (flow == null) return false;

        await _unitOfWork.Flows.DeleteAsync(flow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

public class DeployFlowCommandHandler : IRequestHandler<DeployFlowCommand, DeployFlowResult?>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeployFlowCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DeployFlowResult?> Handle(DeployFlowCommand request, CancellationToken cancellationToken)
    {
        var flow = await _unitOfWork.Flows.GetByIdAsync(request.Id, cancellationToken);
        if (flow == null) return null;

        flow.Deployed = request.Deployed;
        if (request.Deployed)
        {
            flow.TestMode = false; // Exit test mode when deploying
        }
        flow.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Flows.UpdateAsync(flow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeployFlowResult(flow.Deployed);
    }
}
