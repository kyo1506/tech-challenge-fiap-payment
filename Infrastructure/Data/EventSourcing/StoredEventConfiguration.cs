using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Data.EventSourcing;

public class StoredEventConfiguration : IEntityTypeConfiguration<StoredEvent>
{
    public void Configure(EntityTypeBuilder<StoredEvent> builder)
    {
        builder.ToTable("Events");
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => new { e.StreamId, e.Version }).IsUnique();

        builder.Property(e => e.EventType).IsRequired().HasMaxLength(255);
        builder.Property(e => e.Data).IsRequired();
    }
}
