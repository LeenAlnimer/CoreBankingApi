using System.Security.Claims;
using System.Text.Json;
using BankTask.Application.Caching;
using BankTask.Application.DTOs.Accounts;
using BankTask.Application.Events;
using BankTask.Application.Interfaces.Repositories;
using BankTask.Application.Interfaces.Services;
using BankTask.Application.Mappers;
using BankTask.Domain.Entities;
using BankTask.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace BankTask.Application.Services;

public class AccountService : IAccountService
{
    private readonly IAccountRepository _accountRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICacheService _cacheService;
    private readonly IRabbitMqPublisher _rabbitMqPublisher;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccountService(
        IAccountRepository accountRepository,
        IAuditLogRepository auditLogRepository,
        ICacheService cacheService,
        IRabbitMqPublisher rabbitMqPublisher,
        IHttpContextAccessor httpContextAccessor)
    {
        _accountRepository = accountRepository;
        _auditLogRepository = auditLogRepository;
        _cacheService = cacheService;
        _rabbitMqPublisher = rabbitMqPublisher;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<AccountResponse?> GetByIdAsync(Guid id)
    {
        var cacheKey = CacheKeys.AccountById(id);
        var cachedAccount =
            await _cacheService.GetAsync<AccountResponse>(cacheKey);

        if (cachedAccount is not null)
        {
            return cachedAccount;
        }

        var account = await _accountRepository.GetByIdAsync(id);

        if (account is null)
        {
            throw new BankTask.Application.Exceptions.NotFoundException(
                "ACCOUNT_NOT_FOUND");
        }

        var response = AccountMapper.ToResponse(account);
        await _cacheService.SetAsync(
            cacheKey,
            response,
            TimeSpan.FromMinutes(5));

        return response;
    }

    public async Task<IEnumerable<AccountResponse>> GetAllAsync()
    {
        var cachedAccounts =
            await _cacheService.GetAsync<List<AccountResponse>>(
                CacheKeys.AllAccounts);

        if (cachedAccounts is not null)
        {
            return cachedAccounts;
        }

        var accounts = await _accountRepository.GetAllAsync();
        var responses = accounts
            .Select(AccountMapper.ToResponse)
            .ToList();

        await _cacheService.SetAsync(
            CacheKeys.AllAccounts,
            responses,
            TimeSpan.FromMinutes(5));

        return responses;
    }

    public async Task<AccountResponse> CreateAsync(
        CreateAccountRequest request)
    {
        var accountNumber =
            await _accountRepository.GetNextAccountNumberAsync();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            AccountNumber = accountNumber,
            Balance = 0,
            Currency = request.Currency,
            Status = AccountStatus.Active,
            AccountType = request.AccountType,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };

        var createdAccount =
            await _accountRepository.CreateAsync(account);

        await PublishCacheInvalidationAsync(CacheKeys.AllAccounts);

        await CreateAuditLogAsync(
            action: "ACCOUNT_CREATED",
            entityId: createdAccount.Id,
            oldValues: null,
            newValues: new
            {
                createdAccount.Id,
                createdAccount.UserId,
                createdAccount.AccountNumber,
                createdAccount.Balance,
                createdAccount.Currency,
                createdAccount.Status,
                createdAccount.AccountType,
                createdAccount.CreatedAt
            });

        return AccountMapper.ToResponse(createdAccount);
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        UpdateAccountRequest request)
    {
        var existingAccount =
            await _accountRepository.GetByIdAsync(id);

        if (existingAccount is null)
        {
            throw new BankTask.Application.Exceptions.NotFoundException(
                "ACCOUNT_NOT_FOUND");
        }

        var oldStatus = existingAccount.Status;

        existingAccount.Status = request.Status;
        existingAccount.UpdatedAt = DateTime.UtcNow;

        var updated =
            await _accountRepository.UpdateAsync(existingAccount);

        if (updated)
        {
            await PublishCacheInvalidationAsync(
                CacheKeys.AccountById(existingAccount.Id),
                CacheKeys.AllAccounts);

            await CreateAuditLogAsync(
                action: "ACCOUNT_UPDATED",
                entityId: existingAccount.Id,
                oldValues: new
                {
                    existingAccount.Id,
                    Status = oldStatus
                },
                newValues: new
                {
                    existingAccount.Id,
                    existingAccount.Status,
                    existingAccount.UpdatedAt
                });
        }

        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var existingAccount =
            await _accountRepository.GetByIdAsync(id);

        if (existingAccount is null)
        {
            throw new BankTask.Application.Exceptions.NotFoundException(
                "ACCOUNT_NOT_FOUND");
        }

        var oldStatus = existingAccount.Status;

        existingAccount.Status = AccountStatus.Closed;
        existingAccount.UpdatedAt = DateTime.UtcNow;

        var deleted =
            await _accountRepository.UpdateAsync(existingAccount);

        if (deleted)
        {
            await PublishCacheInvalidationAsync(
                CacheKeys.AccountById(existingAccount.Id),
                CacheKeys.AllAccounts);

            await CreateAuditLogAsync(
                action: "ACCOUNT_DELETED",
                entityId: existingAccount.Id,
                oldValues: new
                {
                    existingAccount.Id,
                    existingAccount.UserId,
                    existingAccount.AccountNumber,
                    existingAccount.Balance,
                    existingAccount.Currency,
                    Status = oldStatus,
                    existingAccount.AccountType
                },
                newValues: new
                {
                    existingAccount.Id,
                    existingAccount.Status,
                    existingAccount.UpdatedAt
                });
        }

        return deleted;
    }

    private Task PublishCacheInvalidationAsync(params string[] keys) =>
        _rabbitMqPublisher.PublishAsync(
            CacheInvalidationEvent.QueueName,
            new CacheInvalidationEvent
            {
                EventId = Guid.NewGuid(),
                Keys = keys.Distinct(StringComparer.Ordinal).ToList()
            });

    private async Task CreateAuditLogAsync(
        string action,
        Guid entityId,
        object? oldValues,
        object? newValues)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var userIdClaim =
            httpContext?.User.FindFirst(ClaimTypes.NameIdentifier);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            UserId =
                Guid.TryParse(userIdClaim?.Value, out var userId)
                    ? userId
                    : null,
            Action = action,
            EntityType = "Account",
            EntityId = entityId,
            OldValues =
                oldValues is null
                    ? null
                    : JsonSerializer.Serialize(oldValues),
            NewValues =
                newValues is null
                    ? null
                    : JsonSerializer.Serialize(newValues),
            IpAddress =
                httpContext?.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _auditLogRepository.CreateAsync(auditLog);
    }
}