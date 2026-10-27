
using System.Security.Claims;
using System.Text.Json;
using BankTask.Application.Caching;
using BankTask.Application.Events;
using BankTask.Application.DTOs.Transactions;
using BankTask.Application.Interfaces.Repositories;
using BankTask.Application.Interfaces.Services;
using BankTask.Application.Mappers;
using BankTask.Domain.Entities;
using BankTask.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BankTask.Application.Services;

public class TransactionService : ITransactionService
{
    private const string TransactionCreatedQueue = "transaction-created";
    private const string TransactionEmailQueue = "transaction-email";
    private const string LargeTransactionAlertQueue = "large-transaction-alert";

    private readonly decimal _largeTransactionThreshold;

    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IRabbitMqPublisher _rabbitMqPublisher;
    private readonly ICacheService _cacheService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository,
        IAuditLogRepository auditLogRepository,
        IRabbitMqPublisher rabbitMqPublisher,
        ICacheService cacheService,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<TransactionService> logger)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _auditLogRepository = auditLogRepository;
        _rabbitMqPublisher = rabbitMqPublisher;
        _cacheService = cacheService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;

        _largeTransactionThreshold =
            configuration.GetValue<decimal>(
                "TransactionAlerts:LargeTransactionThreshold",
                5000m);
    }

    public async Task<TransactionResponse?> GetByIdAsync(Guid id)
    {
        var cacheKey = CacheKeys.TransactionById(id);
        var cachedTransaction =
            await _cacheService.GetAsync<TransactionResponse>(cacheKey);

        if (cachedTransaction is not null)
        {
            return cachedTransaction;
        }

        var transaction = await _transactionRepository.GetByIdAsync(id);

        if (transaction is null)
        {
            throw new BankTask.Application.Exceptions.NotFoundException(
                "TRANSACTION_NOT_FOUND");
        }

        var response = TransactionMapper.ToResponse(transaction);
        await _cacheService.SetAsync(
            cacheKey,
            response,
            TimeSpan.FromMinutes(5));

        return response;
    }

    public async Task<IEnumerable<TransactionResponse>> GetAllAsync()
    {
        var cachedTransactions =
            await _cacheService.GetAsync<List<TransactionResponse>>(
                CacheKeys.AllTransactions);

        if (cachedTransactions is not null)
        {
            return cachedTransactions;
        }

        var transactions = await _transactionRepository.GetAllAsync();
        var responses = transactions
            .Select(TransactionMapper.ToResponse)
            .ToList();

        await _cacheService.SetAsync(
            CacheKeys.AllTransactions,
            responses,
            TimeSpan.FromMinutes(5));

        return responses;
    }

    public async Task<TransactionResponse> CreateAsync(
        CreateTransactionRequest request)
    {
        ValidateTransaction(request);

        if (request.TransactionType == TransactionType.Transfer
            && request.SourceAccountId == request.DestinationAccountId)
        {
            throw new BankTask.Application.Exceptions.ValidationException(
                "TRANSFER_ACCOUNTS_MUST_BE_DIFFERENT");
        }

        Account? sourceAccount = null;
        Account? destinationAccount = null;

        if (request.SourceAccountId.HasValue)
        {
            sourceAccount = await _accountRepository.GetByIdAsync(
                request.SourceAccountId.Value);

            if (sourceAccount is null)
            {
                throw new BankTask.Application.Exceptions.NotFoundException(
                    "ACCOUNT_NOT_FOUND");
            }

            var httpContext = _httpContextAccessor.HttpContext;

            var userIdClaim =
                httpContext?.User.FindFirst(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdClaim?.Value, out var authenticatedUserId))
            {
                throw new BankTask.Application.Exceptions.UnauthorizedException(
                    "USER_ID_REQUIRED");
            }

            if (sourceAccount.UserId != authenticatedUserId)
            {
                throw new BankTask.Application.Exceptions.UnauthorizedException(
                    "ACCOUNT_ACCESS_FORBIDDEN");
            }
        }

        if (request.DestinationAccountId.HasValue)
        {
            destinationAccount = await _accountRepository.GetByIdAsync(
                request.DestinationAccountId.Value);

            if (destinationAccount is null)
            {
                throw new BankTask.Application.Exceptions.NotFoundException(
                    "ACCOUNT_NOT_FOUND");
            }
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),

            SourceAccountId = request.SourceAccountId,
            DestinationAccountId = request.DestinationAccountId,

            TransactionType = (short)request.TransactionType,

            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),

            ReferenceNumber = GenerateReferenceNumber(),

            Description = request.Description?.Trim(),

            CreatedAt = DateTimeOffset.UtcNow
        };

        var createdTransaction =
            await _transactionRepository.CreateAsync(transaction);

        _logger.LogInformation(
            "Transaction created successfully. EventId: {EventId}, TransactionId: {TransactionId}, Amount: {Amount}, Currency: {Currency}",
            createdTransaction.EventId,
            createdTransaction.Id,
            createdTransaction.Amount,
            createdTransaction.Currency);

        await _rabbitMqPublisher.PublishAsync(
            CacheInvalidationEvent.QueueName,
            new CacheInvalidationEvent
            {
                EventId = Guid.NewGuid(),
                Keys = new List<string>
                {
                    CacheKeys.AllTransactions,
                    CacheKeys.TransactionById(createdTransaction.Id)
                }
            });

        await CreateAuditLogAsync(
            action: "TRANSACTION_CREATED",
            eventId: createdTransaction.EventId,
            entityId: createdTransaction.Id,
            newValues: new
            {
                createdTransaction.Id,
                createdTransaction.EventId,
                createdTransaction.SourceAccountId,
                createdTransaction.DestinationAccountId,
                createdTransaction.TransactionType,
                createdTransaction.Amount,
                createdTransaction.Currency,
                createdTransaction.ReferenceNumber,
                createdTransaction.Description,
                createdTransaction.CreatedAt
            });

        var recipientUserIds = new HashSet<Guid>();

        switch (request.TransactionType)
        {
            case TransactionType.Transfer:
                if (sourceAccount is not null)
                {
                    recipientUserIds.Add(sourceAccount.UserId);
                }

                if (destinationAccount is not null)
                {
                    recipientUserIds.Add(destinationAccount.UserId);
                }

                break;

            case TransactionType.Deposit:
                if (destinationAccount is not null)
                {
                    recipientUserIds.Add(destinationAccount.UserId);
                }

                break;

            case TransactionType.Withdrawal:
                if (sourceAccount is not null)
                {
                    recipientUserIds.Add(sourceAccount.UserId);
                }

                break;
        }

        var transactionTypeName =
            ((TransactionType)createdTransaction.TransactionType).ToString();

        var transactionCreatedEvent = new TransactionCreatedEvent
        {
            EventId = createdTransaction.EventId,
            TransactionId = createdTransaction.Id,
            SourceAccountId = createdTransaction.SourceAccountId,
            DestinationAccountId = createdTransaction.DestinationAccountId,
            TransactionType = transactionTypeName,
            Amount = createdTransaction.Amount,
            Currency = createdTransaction.Currency,
            ReferenceNumber = createdTransaction.ReferenceNumber,
            CreatedAt = createdTransaction.CreatedAt,
            RecipientUserIds = recipientUserIds.ToList()
        };

        _logger.LogInformation(
            "Publishing SignalR event {SignalREventName} to RabbitMQ queue {QueueName}. EventId: {EventId}, TransactionId: {TransactionId}, RecipientCount: {RecipientCount}",
            "TransactionCreated",
            TransactionCreatedQueue,
            transactionCreatedEvent.EventId,
            transactionCreatedEvent.TransactionId,
            transactionCreatedEvent.RecipientUserIds.Count);

        await _rabbitMqPublisher.PublishAsync(
            TransactionCreatedQueue,
            transactionCreatedEvent);

        _logger.LogInformation(
            "Published SignalR event {SignalREventName} to RabbitMQ queue {QueueName}. TransactionId: {TransactionId}",
            "TransactionCreated",
            TransactionCreatedQueue,
            transactionCreatedEvent.TransactionId);

        // Publish a separate event for transaction emails.
        var transactionEmailEvent = new TransactionEmailEvent
        {
            EventId = createdTransaction.EventId,
            TransactionId = createdTransaction.Id,
            TransactionType = transactionTypeName,
            Amount = createdTransaction.Amount,
            Currency = createdTransaction.Currency,
            ReferenceNumber = createdTransaction.ReferenceNumber,
            CreatedAt = createdTransaction.CreatedAt,
            RecipientUserIds = recipientUserIds.ToList()
        };

        await _rabbitMqPublisher.PublishAsync(
            TransactionEmailQueue,
            transactionEmailEvent);

        // Publish a separate alert for large transactions in JOD.
        if (string.Equals(
                createdTransaction.Currency,
                "JOD",
                StringComparison.OrdinalIgnoreCase)
            && createdTransaction.Amount >= _largeTransactionThreshold)
        {
            var largeTransactionAlertEvent = new LargeTransactionAlertEvent
            {
                EventId = Guid.NewGuid(),
                TransactionId = createdTransaction.Id,
                SourceAccountId = createdTransaction.SourceAccountId,
                DestinationAccountId = createdTransaction.DestinationAccountId,
                TransactionType = transactionTypeName,
                Amount = createdTransaction.Amount,
                Currency = createdTransaction.Currency,
                ReferenceNumber = createdTransaction.ReferenceNumber,
                CreatedAt = createdTransaction.CreatedAt,
                RecipientUserIds = recipientUserIds.ToList()
            };

            _logger.LogInformation(
                "Publishing SignalR event {SignalREventName} to RabbitMQ queue {QueueName}. EventId: {EventId}, TransactionId: {TransactionId}, RecipientCount: {RecipientCount}",
                "LargeTransactionAlert",
                LargeTransactionAlertQueue,
                largeTransactionAlertEvent.EventId,
                largeTransactionAlertEvent.TransactionId,
                largeTransactionAlertEvent.RecipientUserIds.Count);

            await _rabbitMqPublisher.PublishAsync(
                LargeTransactionAlertQueue,
                largeTransactionAlertEvent);

            _logger.LogInformation(
                "Published SignalR event {SignalREventName} to RabbitMQ queue {QueueName}. TransactionId: {TransactionId}",
                "LargeTransactionAlert",
                LargeTransactionAlertQueue,
                largeTransactionAlertEvent.TransactionId);
        }

        return TransactionMapper.ToResponse(createdTransaction);
    }

    private static void ValidateTransaction(
        CreateTransactionRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new BankTask.Application.Exceptions.ValidationException(
                "TRANSACTION_AMOUNT_INVALID");
        }

        switch (request.TransactionType)
        {
            case TransactionType.Transfer:
                if (request.SourceAccountId is null ||
                    request.DestinationAccountId is null)
                {
                    throw new BankTask.Application.Exceptions.ValidationException(
                        "TRANSFER_ACCOUNTS_REQUIRED");
                }

                if (request.SourceAccountId == request.DestinationAccountId)
                {
                    throw new BankTask.Application.Exceptions.ValidationException(
                        "TRANSFER_ACCOUNTS_MUST_DIFFER");
                }

                break;

            case TransactionType.Deposit:
                if (request.DestinationAccountId is null)
                {
                    throw new BankTask.Application.Exceptions.ValidationException(
                        "DEPOSIT_DESTINATION_REQUIRED");
                }

                break;

            case TransactionType.Withdrawal:
                if (request.SourceAccountId is null)
                {
                    throw new BankTask.Application.Exceptions.ValidationException(
                        "WITHDRAWAL_SOURCE_REQUIRED");
                }

                break;

            default:
                throw new BankTask.Application.Exceptions.ValidationException(
                    "TRANSACTION_TYPE_INVALID");
        }

        if (string.IsNullOrWhiteSpace(request.Currency) ||
            request.Currency.Trim().Length != 3)
        {
            throw new BankTask.Application.Exceptions.ValidationException(
                "CURRENCY_INVALID");
        }
    }

    private static string GenerateReferenceNumber()
    {
        return $"TX-{Guid.NewGuid():N}";
    }

    private async Task CreateAuditLogAsync(
        string action,
        Guid eventId,
        Guid entityId,
        object newValues)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var userIdClaim =
            httpContext?.User.FindFirst(ClaimTypes.NameIdentifier);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId =
                Guid.TryParse(userIdClaim?.Value, out var userId)
                    ? userId
                    : null,
            Action = action,
            EntityType = "Transaction",
            EntityId = entityId,
            OldValues = null,
            NewValues = JsonSerializer.Serialize(newValues),
            IpAddress =
                httpContext?.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _auditLogRepository.CreateAsync(auditLog);
    }
}
