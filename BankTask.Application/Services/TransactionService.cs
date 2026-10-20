
using System.Security.Claims;
using System.Text.Json;
using BankTask.Application.Events;
using BankTask.Application.DTOs.Transactions;
using BankTask.Application.Interfaces.Repositories;
using BankTask.Application.Interfaces.Services;
using BankTask.Application.Mappers;
using BankTask.Domain.Entities;
using BankTask.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace BankTask.Application.Services;

public class TransactionService : ITransactionService
{
    private const string TransactionCreatedQueue = "transaction-created";
    private const string TransactionEmailQueue = "transaction-email";

    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IRabbitMqPublisher _rabbitMqPublisher;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TransactionService(
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository,
        IAuditLogRepository auditLogRepository,
        IRabbitMqPublisher rabbitMqPublisher,
        IHttpContextAccessor httpContextAccessor)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _auditLogRepository = auditLogRepository;
        _rabbitMqPublisher = rabbitMqPublisher;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<TransactionResponse?> GetByIdAsync(Guid id)
    {
        var transaction =
            await _transactionRepository.GetByIdAsync(id);

        if (transaction is null)
        {
            throw new BankTask.Application.Exceptions.NotFoundException(
                "TRANSACTION_NOT_FOUND");
        }

        return TransactionMapper.ToResponse(transaction);
    }

    public async Task<IEnumerable<TransactionResponse>> GetAllAsync()
    {
        var transactions =
            await _transactionRepository.GetAllAsync();

        return transactions.Select(TransactionMapper.ToResponse);
    }

    public async Task<TransactionResponse> CreateAsync(
        CreateTransactionRequest request)
    {
        ValidateTransaction(request);

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

        var transactionCreatedEvent = new TransactionCreatedEvent
        {
            EventId = createdTransaction.EventId,
            TransactionId = createdTransaction.Id,
            SourceAccountId = createdTransaction.SourceAccountId,
            DestinationAccountId = createdTransaction.DestinationAccountId,
            TransactionType =
                ((TransactionType)createdTransaction.TransactionType).ToString(),
            Amount = createdTransaction.Amount,
            Currency = createdTransaction.Currency,
            ReferenceNumber = createdTransaction.ReferenceNumber,
            CreatedAt = createdTransaction.CreatedAt,
            RecipientUserIds = recipientUserIds.ToList()
        };

        // Publish the event for SignalR notifications.
        await _rabbitMqPublisher.PublishAsync(
            TransactionCreatedQueue,
            transactionCreatedEvent);

        // Publish a separate event for transaction emails.
        var transactionEmailEvent = new TransactionEmailEvent
        {
            EventId = createdTransaction.EventId,
            TransactionId = createdTransaction.Id,
            TransactionType =
                ((TransactionType)createdTransaction.TransactionType).ToString(),
            Amount = createdTransaction.Amount,
            Currency = createdTransaction.Currency,
            ReferenceNumber = createdTransaction.ReferenceNumber,
            CreatedAt = createdTransaction.CreatedAt,
            RecipientUserIds = recipientUserIds.ToList()
        };

        await _rabbitMqPublisher.PublishAsync(
            TransactionEmailQueue,
            transactionEmailEvent);

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
