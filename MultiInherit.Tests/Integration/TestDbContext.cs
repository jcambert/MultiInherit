using Microsoft.EntityFrameworkCore;
using MultiInherit.EFCore;

namespace MultiInherit.Tests.Integration;

/// <summary>
/// Minimal DbContext for integration tests.
/// Inherits <see cref="ModelDbContext"/> which auto-maps all registered models.
/// </summary>
public sealed class TestDbContext(DbContextOptions options) : ModelDbContext(options)
{
}
