using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MultiInherit.EFCore;

public static class TableBuilderExtensions
{
    extension<TEntity>(EntityTypeBuilder<TEntity> entityTypeBuilder) where TEntity : class
    {
        public EntityTypeBuilder<TEntity> ToTableWithNamingConvention(string name, string? schema)
        {
            if (!string.IsNullOrWhiteSpace(schema))
            {
                schema = schema.ToNamingConvention(DatabaseNamingHelper.Options.Value.NamingConvention);
            }
            return entityTypeBuilder.ToTable(DatabaseNamingHelper.ToNameWithNamingConvention(name), schema);
        }
    }

    extension(OwnedNavigationBuilder ownedNavigationBuilder)
    {
        /// <summary>
        /// Configures the table name and optional schema for the owned entity using the current database naming
        /// convention.
        /// </summary>
        /// <remarks>If the schema parameter is provided and not null or whitespace, it is converted to
        /// match the current naming convention before being applied. This method is useful for ensuring consistent
        /// table and schema naming across the database model.</remarks>
        /// <param name="name">The base name to use for the table. The name will be transformed according to the configured naming
        /// convention.</param>
        /// <param name="schema">An optional schema name for the table. If specified and not null or whitespace, the schema will be
        /// transformed according to the configured naming convention.</param>
        /// <returns>An OwnedNavigationBuilder instance configured with the specified table name and schema using the naming
        /// convention.</returns>
        public OwnedNavigationBuilder ToTableWithNamingConvention(string name, string? schema = null)
        {
            if (!string.IsNullOrWhiteSpace(schema))
            {
                schema = schema.ToNamingConvention(DatabaseNamingHelper.Options.Value.NamingConvention);
            }

            return ownedNavigationBuilder.ToTable(DatabaseNamingHelper.ToNameWithNamingConvention(name), schema);
        }
    }

}