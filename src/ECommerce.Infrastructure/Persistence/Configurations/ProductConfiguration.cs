using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        EntityBaseConfiguration.ConfigureBaseProperties(builder);

        builder.ToTable("Products");

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.Sku).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.DiscountPercentage).HasPrecision(5, 2).HasDefaultValue(0m);
        builder.Property(x => x.StockQuantity).HasDefaultValue(0);
        builder.Property(x => x.LowStockThreshold).HasDefaultValue(5);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.IsFeatured).HasDefaultValue(false);
        builder.Property(x => x.AverageRating).HasPrecision(3, 2).HasDefaultValue(0m);
        builder.Property(x => x.ReviewCount).HasDefaultValue(0);

        // Money is stored as an owned pair of columns so the currency survives a round trip.
        builder.OwnsOne(x => x.Price, money =>
        {
            money.Property(x => x.Amount).HasColumnName("Price").HasPrecision(18, 2).IsRequired();
            money.Property(x => x.Currency).HasColumnName("PriceCurrency").HasMaxLength(3).IsRequired();
        });

        builder.OwnsOne(x => x.CompareAtPrice, money =>
        {
            money.Property(x => x.Amount).HasColumnName("CompareAtPrice").HasPrecision(18, 2);
            money.Property(x => x.Currency).HasColumnName("CompareAtPriceCurrency").HasMaxLength(3);
        });

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Sku).IsUnique();
        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => new { x.CategoryId, x.IsActive, x.IsDeleted });
    }
}