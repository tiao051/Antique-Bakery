using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using highlands.Models.DTO.ReportDTO;

public class ReportDocument : IDocument
{
    private readonly ReportData _data;

    public ReportDocument(ReportData data)
    {
        _data = data;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(30);

            page.Header().Text($"📊 REPORT {_data.ReportType.ToUpper()} ({_data.TimeRangeText})")
                .FontSize(18).Bold().FontColor(Colors.Blue.Medium);

            page.Content().PaddingVertical(10).Column(col =>
            {
                // Total revenue
                col.Item().Text($"🧾 Total Revenue: {_data.TotalRevenue:N0} $")
                    .FontSize(14).Bold().FontColor(Colors.Green.Darken2);

                // Best sellers
                if (_data.BestSellers == null || !_data.BestSellers.Any())
                {
                    col.Item().Text("No best-selling products available in this period.");
                }
                else
                {
                    col.Item().PaddingTop(15).Text("🔥 Top 5 Best-Selling Products:").FontSize(13).Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(80); });

                        table.Header(header =>
                        {
                            header.Cell().Text("Product Name").Bold();
                            header.Cell().AlignRight().Text("Quantity Sold").Bold();
                        });

                        foreach (var p in _data.BestSellers)
                        {
                            table.Cell().Text(p.Name);
                            table.Cell().AlignRight().Text(p.Quantity.ToString());
                        }
                    });
                }

                // Worst sellers
                col.Item().PaddingTop(15).Text("❄️ Top 5 Worst-Selling Products:").FontSize(13).Bold();
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(80); });

                    table.Header(header =>
                    {
                        header.Cell().Text("Product Name").Bold();
                        header.Cell().AlignRight().Text("Quantity Sold").Bold();
                    });

                    foreach (var p in _data.WorstSellers)
                    {
                        table.Cell().Text(p.Name);
                        table.Cell().AlignRight().Text(p.Quantity.ToString());
                    }
                });

                // Revenue by category
                col.Item().PaddingTop(15).Text("📂 Revenue by Category:").FontSize(13).Bold();
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(100); });

                    table.Header(header =>
                    {
                        header.Cell().Text("Category").Bold();
                        header.Cell().AlignRight().Text("Revenue ($)").Bold();
                    });

                    foreach (var cat in _data.RevenueByCategory)
                    {
                        table.Cell().Text(cat.Category);
                        table.Cell().AlignRight().Text(cat.Revenue.ToString("N0"));
                    }
                });

                // Revenue by product
                col.Item().PaddingTop(15).Text("📦 Revenue by Product:").FontSize(13).Bold();
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(100); });

                    table.Header(header =>
                    {
                        header.Cell().Text("Product").Bold();
                        header.Cell().AlignRight().Text("Revenue ($)").Bold();
                    });

                    foreach (var p in _data.RevenueByProduct)
                    {
                        table.Cell().Text(p.Name);
                        table.Cell().AlignRight().Text(p.Revenue.ToString("N0"));
                    }
                });

                // Top customers
                col.Item().PaddingTop(15).Text("👥 Top Customers:").FontSize(13).Bold();
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn();
                        c.ConstantColumn(80);
                        c.ConstantColumn(100);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Customer Name").Bold();
                        header.Cell().AlignRight().Text("Order Count").Bold();
                        header.Cell().AlignRight().Text("Total Spent ($)").Bold();
                    });

                    foreach (var c in _data.TopCustomers)
                    {
                        table.Cell().Text(c.CustomerName);
                        table.Cell().AlignRight().Text($"{c.OrderCount}");
                        table.Cell().AlignRight().Text($"{c.TotalSpent:N0}");
                    }
                });

                // Peak and off-peak hours
                col.Item().PaddingTop(15).Text("⏰ Hourly Sales Report:").FontSize(13).Bold();

                col.Item().Text(text =>
                {
                    text.Span("👉 Peak Sales Hour: ").Bold();
                    text.Span($"{_data.PeakTime.TimeRange}").SemiBold();
                });

                col.Item().Text(text =>
                {
                    text.Span("🕑 Off-Peak Sales Hour: ").Bold();
                    text.Span($"{_data.OffTime.TimeRange}").SemiBold();
                });
            });

            // Footer
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Generated by Antique System").FontSize(10);
                text.Span(" | Page ").FontSize(10);
                text.CurrentPageNumber().FontSize(10);
            });
        });
    }
}
