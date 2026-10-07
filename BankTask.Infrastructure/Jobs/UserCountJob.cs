using BankTask.Application.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace BankTask.Infrastructure.Jobs;

public class UserCountJob
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserCountJob> _logger;

    public UserCountJob(
        IUserRepository userRepository,
        ILogger<UserCountJob> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var usersCount =
            await _userRepository.GetUsersCountAsync();

        _logger.LogInformation(
            "Current users count: {UsersCount}",
            usersCount);
    }
}