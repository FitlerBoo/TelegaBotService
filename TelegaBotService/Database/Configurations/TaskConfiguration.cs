using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TelegaBotService.Database.Entities;

namespace TelegaBotService.Database.Configurations
{
    public class TaskConfiguration : IEntityTypeConfiguration<TaskEntity>
    {
        public void Configure(EntityTypeBuilder<TaskEntity> builder)
        {
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Date)
                .IsRequired();
            builder.Property(t => t.Type)
                .IsRequired();
            builder.Property(t => t.Description)
                .IsRequired();
            builder.Property(t => t.Location)
                .IsRequired();
            builder.Property(t => t.Performers)
                .IsRequired();
        }
    }
}
