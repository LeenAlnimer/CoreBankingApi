using System.Data;
using BankTask.Application.Interfaces.Repositories;
using BankTask.DBManager;
using BankTask.Domain.Entities;
using Dapper;

namespace BankTask.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IConnectionFactory _connectionFactory;

    private const string GetByIdSp = "GetUserById";
    private const string GetByEmailSp = "GetUserByEmail";
    private const string GetAllSp = "GetAllUsers";
    private const string GetUsersCountSp = "GetUsersCount";
    private const string CreateSp = "CreateUser";
    private const string UpdateSp = "UpdateUser";
    private const string DeleteSp = "DeleteUser";

    public UserRepository(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private IDbConnection CreateConnection() =>
        _connectionFactory.CreateConnection(DatabaseType.SqlServer);

    public async Task<User?> GetByIdAsync(Guid id)
    {
        using var connection = CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<User>(
            GetByIdSp,
            new { Id = id },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        using var connection = CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<User>(
            GetByEmailSp,
            new { Email = email },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        using var connection = CreateConnection();

        return await connection.QueryAsync<User>(
            GetAllSp,
            commandType: CommandType.StoredProcedure);
    }

    public async Task<int> GetUsersCountAsync()
    {
        using var connection = CreateConnection();

        return await connection.QuerySingleAsync<int>(
            GetUsersCountSp,
            commandType: CommandType.StoredProcedure);
    }

    public async Task<User> CreateAsync(User user)
    {
        using var connection = CreateConnection();

        return await connection.QuerySingleAsync<User>(
            CreateSp,
            new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.PasswordHash,
                user.CreatedAt,
                user.UpdatedAt
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<bool> UpdateAsync(User user)
    {
        using var connection = CreateConnection();

        var rowsAffected = await connection.QuerySingleAsync<int>(
            UpdateSp,
            new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.UpdatedAt
            },
            commandType: CommandType.StoredProcedure);

        return rowsAffected == 1;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var connection = CreateConnection();

        var rowsAffected = await connection.QuerySingleAsync<int>(
            DeleteSp,
            new { Id = id },
            commandType: CommandType.StoredProcedure);

        return rowsAffected == 1;
    }
}