using Microsoft.EntityFrameworkCore.Storage;
using DataForeman.Api.Data;

namespace DataForeman.Api.Repositories;

/// <summary>
/// Unit of Work implementation for managing transactions and repositories
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly DataForemanDbContext _context;
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    private IFlowRepository? _flows;
    private IChartRepository? _charts;
    private IDashboardRepository? _dashboards;
    private IConnectionRepository? _connections;
    private ITagRepository? _tags;
    private IUserRepository? _users;
    private ISessionRepository? _sessions;

    public UnitOfWork(DataForemanDbContext context)
    {
        _context = context;
    }

    public IFlowRepository Flows => _flows ??= new FlowRepository(_context);
    public IChartRepository Charts => _charts ??= new ChartRepository(_context);
    public IDashboardRepository Dashboards => _dashboards ??= new DashboardRepository(_context);
    public IConnectionRepository Connections => _connections ??= new ConnectionRepository(_context);
    public ITagRepository Tags => _tags ??= new TagRepository(_context);
    public IUserRepository Users => _users ??= new UserRepository(_context);
    public ISessionRepository Sessions => _sessions ??= new SessionRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
        _disposed = true;
    }
}
