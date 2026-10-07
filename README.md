# CoreBankingApi

## Overview

CoreBankingApi is a backend banking application built with ASP.NET Core and C#.

The project is designed as a practical backend task to demonstrate clean separation of responsibilities, database abstraction, repository-based data access, authentication, and integration with multiple database technologies.

The banking business rules are intentionally kept simple. The main focus of the project is the backend structure, implementation approach, database separation, and maintainability.

---

## Main Features

The application is planned to support the following features:

- User management
- Account management
- Transaction management
- Audit logging
- User registration
- User login
- JWT authentication
- Protected API endpoints
- CRUD operations where applicable
- Database access through repositories
- Support for multiple database technologies
- Database-specific connection management
- Fluent validation
- Using Serilog for Logging and seq sink
- Using  Public API for Exchange  Rate
- Hangfire for Email  Service and  UserCount
- Redis Cache
- API documentation through Swagger/OpenAPI

---

## Database Architecture

The application uses two different database technologies.

### SQL Server

SQL Server is used for:

- Users
- Accounts

Database:

`BankTaskDB`

SQL Server is running locally through Docker.

### PostgreSQL

PostgreSQL is used for:

- Transactions
- Audit Logs

Database:

`bankdb`

PostgreSQL is also running locally through Docker.

### Database Separation

The database responsibilities are separated as follows:

SQL Server:
- Users
- Accounts

PostgreSQL:
- Transactions
- Audit Logs

The application demonstrates how a single backend application can communicate with multiple databases while keeping the database-specific implementation isolated.

---

## Database Tables

### Users

The Users table contains:

- Id
- FullName
- Email
- PasswordHash
- CreatedAt
- UpdatedAt

The Email column is unique.

### Accounts

The Accounts table contains:

- Id
- UserId
- AccountNumber
- Balance
- Currency
- Status
- AccountType
- CreatedAt
- UpdatedAt

Each account belongs to a user.

The AccountNumber column is unique.

### Transactions

The Transactions table contains:

- id
- event_id
- source_account_id
- destination_account_id
- transaction_type
- amount
- currency
- reference_number
- description
- created_at

The EventId and ReferenceNumber values are unique.

Transactions are stored in PostgreSQL.

### Audit Logs

The AuditLogs table contains:

- id
- event_id
- user_id
- action
- entity_type
- entity_id
- old_values
- new_values
- ip_address
- created_at

Audit logs are stored in PostgreSQL.

JSONB is used for storing old and new values.

---

## Project Architecture

The solution is divided into multiple projects to separate responsibilities.

### BankTask.Domain

The Domain project contains the main business entities of the application.

Current entities include:

- User
- Account
- Transaction
- AuditLog

The Domain layer is kept independent from infrastructure-specific technologies such as SQL Server, PostgreSQL, Dapper, or ASP.NET Core.

### BankTask.Application

The Application project is responsible for application-level logic and abstractions.

It contains application contracts and services that coordinate operations between the API and Infrastructure layers.

The goal is to keep application logic separated from database implementation details.

### BankTask.Infrastructure

The Infrastructure project contains the implementation of data access.

Dapper is used as the micro ORM for database operations.

Current repositories include:

- UserRepository
- AccountRepository
- TransactionRepository
- AuditLogRepository

Repositories are responsible for executing SQL queries and communicating with the appropriate database manager.

### BankTask.DBManager

The DBManager project is responsible for database connection creation.

The project supports two database technologies:

- SQL Server
- PostgreSQL

The main abstraction is:

`IDbManager`

The SQL Server implementation is:

`SqlServerDbManager`

The PostgreSQL implementation is:

`PostgreSqlDbManager`

Both implementations create database connections through the common `IDbConnection` abstraction.

### BankTask.Authentication

The Authentication project contains authentication-related functionality.

The current implementation includes:

- JwtService

Authentication is implemented using JWT.

### BankTask.Api

The API project is the entry point of the application.

It is responsible for:

- HTTP requests
- Controllers
- Routing
- Dependency Injection configuration
- Middleware pipeline
- Authentication configuration
- Swagger/OpenAPI

The API layer should not contain direct SQL queries.

---

## Factory Pattern

The project uses the Factory Pattern for creating the appropriate database manager.

The factory is:

`DbManagerFactory`

Its responsibility is to select the correct database manager based on the requested database type.

The general flow is:

Application or API configuration

↓

DbManagerFactory

↓

SqlServerDbManager or PostgreSqlDbManager

↓

Database Connection

This keeps database-specific object creation centralized instead of spreading creation logic throughout the application.

---

## Repository Pattern

The project uses the Repository Pattern for database access.

The general application flow is:

Controller

↓

Application Layer

↓

Repository

↓

DB Manager

↓

Database Connection

↓

Dapper

↓

Database

Repositories hide SQL queries and database access details from controllers and application services.

---

## Dapper

Dapper is used instead of Entity Framework Core.

The main reasons for using Dapper in this project are:

- Direct SQL control
- Lightweight data access
- Explicit queries
- Simple mapping between query results and entities
- Clear visibility of database operations

The project does not use:

- Entity Framework Core
- DbContext
- EF Core migrations

Database tables are created using SQL scripts and database commands.

---

## Authentication

Authentication is implemented in:

`BankTask.Authentication`

The authentication functionality uses JWT.

The planned authentication functionality includes:

- User registration
- User login
- Password hashing
- JWT token generation
- Protected endpoints
- Authorization

The authentication logic is kept separate from the API and database implementation.

---

## Dependency Injection

ASP.NET Core Dependency Injection is used to register database managers, repositories, and application services.

The API project is responsible for configuring the dependency injection container.

The goal is to avoid manually creating dependencies inside controllers and services.

---

## Docker

The databases are running using Docker containers.

Current containers:

- banktask-sqlserver
- banktask-postgres


---

## API Design

The API endpoints will be designed according to the required use cases.

The task allows the API design to be determined during implementation.

Planned areas include:

### Authentication

- Register
- Login

### Users

- Get
- Create
- Update
- Delete

### Accounts

- Get
- Create
- Update
- Delete

### Transactions

- Get
- Create
- Update
- Delete

### Audit Logs

- Get
- Create

The exact endpoint structure may be adjusted according to the final implementation and requirements.

---

## Business Rules

The business rules are intentionally simple.

The focus of the task is not on implementing a complex banking system.

The main focus is:

- Backend architecture
- Separation of responsibilities
- Database abstraction
- Repository pattern
- Multiple database support
- Authentication
- API design
- Maintainable implementation

Specific transaction and account rules will be implemented according to the requirements agreed for the task.

---

## Project Structure

BankTask

- BankTask.Api
  - Controllers
  - Properties
  - Program.cs
  - appsettings.json
  - BankTask.Api.csproj

- BankTask.Application

- BankTask.Authentication
  - JwtService.cs

- BankTask.DBManager
  - IDbManager.cs
  - SqlServerDbManager.cs
  - PostgreSqlDbManager.cs
  - DbManagerFactory.cs

- BankTask.Domain
  - Entities
    - User.cs
    - Account.cs
    - Transaction.cs
    - AuditLog.cs

- BankTask.Infrastructure
  - Repositories
    - UserRepository.cs
    - AccountRepository.cs
    - TransactionRepository.cs
    - AuditLogRepository.cs

- BankTask.sln
- .gitignore
- .gitattributes
- README.md

---

## Technologies

The project currently uses:

- C#
- .NET 8
- ASP.NET Core Web API
- Dapper
- SQL Server
- PostgreSQL
- Docker
- JWT
- Swagger / OpenAPI
- Git
- GitHub

---

## NuGet Packages

The project uses the following main packages:

### SQL Server

`Microsoft.Data.SqlClient`

### PostgreSQL

`Npgsql`

### Data Access

`Dapper`

---

## Configuration and Secrets

Database credentials and other sensitive configuration values should not be committed to GitHub.

For local development, sensitive configuration is kept outside the committed configuration files.

The project uses local secrets/environment-based configuration for sensitive values.

The committed `appsettings.json` should contain only non-sensitive configuration or safe placeholders.




---

## Development Approach

The project is being developed incrementally.

The general implementation sequence is:

1. Solution and project structure
2. Database configuration
3. Docker database containers
4. Database schema
5. Domain entities
6. DBManager abstraction
7. Factory Pattern
8. Repository layer
9. Application layer
10. Authentication
11. API endpoints
12. Business rules
13. Validation
14. Error handling
15. Testing
16. Final cleanup and documentation

Each logical stage is developed and committed separately.

---


