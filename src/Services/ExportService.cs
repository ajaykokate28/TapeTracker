using System.Data;
using System.Text.Json;
using MiniExcelLibs;
using TapeTracker.Models;

namespace TapeTracker.Services;

public class ExportService : IExportService
{
    public async Task ExportAsync(Customer customer, Order order, IUnitPreferenceService unitService)
    {
        var unit = unitService.UnitLabel;

        var export = new
        {
            customer.Name,
            customer.Phone,
            OrderNumber = order.OrderNumber,
            Date        = order.Date.ToString("yyyy-MM-dd"),
            DueDate     = order.DueDate?.ToString("yyyy-MM-dd"),
            Status      = order.Status.ToString(),
            Unit        = unit,
            Shirt = order.Shirt is null ? null : new
            {
                Length   = unitService.ToDisplay(order.Shirt.Length),
                Chest    = unitService.ToDisplay(order.Shirt.Chest),
                Waist    = unitService.ToDisplay(order.Shirt.Waist),
                Hip      = unitService.ToDisplay(order.Shirt.Hip),
                Shoulder = unitService.ToDisplay(order.Shirt.Shoulder),
                Sleeve   = unitService.ToDisplay(order.Shirt.Sleeve),
                Cuff     = unitService.ToDisplay(order.Shirt.Cuff),
                Collar   = unitService.ToDisplay(order.Shirt.Collar)
            },
            Pant = order.Pant is null ? null : new
            {
                Length = unitService.ToDisplay(order.Pant.Length),
                Waist  = unitService.ToDisplay(order.Pant.Waist),
                Hip    = unitService.ToDisplay(order.Pant.Hip),
                Thigh  = unitService.ToDisplay(order.Pant.Thigh),
                Ankle  = unitService.ToDisplay(order.Pant.Ankle),
                Knee   = unitService.ToDisplay(order.Pant.Knee),
                Seat   = unitService.ToDisplay(order.Pant.Seat)
            }
        };

        var json     = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        var safeName = string.Concat(customer.Name.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{safeName}_{order.OrderNumber}_{order.Date:yyyyMMdd}.json";

        var dir = Path.Combine(FileSystem.AppDataDirectory, "exports");
        Directory.CreateDirectory(dir);

        var filePath = Path.Combine(dir, fileName);
        await File.WriteAllTextAsync(filePath, json);

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = $"Measurements â€“ {customer.Name} / {order.OrderNumber}",
            File  = new ShareFile(filePath)
        });
    }

    public async Task ExportAllToExcelAsync(IEnumerable<Customer> customers, IUnitPreferenceService unitService)
    {
        var unit = unitService.UnitLabel;

        var dt = new DataTable();
        dt.Columns.Add("Name");
        dt.Columns.Add("Phone");
        dt.Columns.Add("Order #");
        dt.Columns.Add("Date");
        dt.Columns.Add("Due Date");
        dt.Columns.Add("Status");
        dt.Columns.Add($"Shirt Length ({unit})");
        dt.Columns.Add($"Shirt Chest ({unit})");
        dt.Columns.Add($"Shirt Waist ({unit})");
        dt.Columns.Add($"Shirt Hip ({unit})");
        dt.Columns.Add($"Shirt Shoulder ({unit})");
        dt.Columns.Add($"Shirt Sleeve ({unit})");
        dt.Columns.Add($"Shirt Cuff ({unit})");
        dt.Columns.Add($"Shirt Collar ({unit})");
        dt.Columns.Add($"Pant Length ({unit})");
        dt.Columns.Add($"Pant Waist ({unit})");
        dt.Columns.Add($"Pant Hip ({unit})");
        dt.Columns.Add($"Pant Thigh ({unit})");
        dt.Columns.Add($"Pant Ankle ({unit})");
        dt.Columns.Add($"Pant Knee ({unit})");
        dt.Columns.Add($"Pant Seat ({unit})");

        foreach (var c in customers)
        {
            foreach (var o in c.Orders.OrderByDescending(x => x.Date))
            {
                string D(decimal? v) => v.HasValue ? unitService.ToDisplay(v.Value).ToString("0.##") : "";
                dt.Rows.Add(
                    c.Name, c.Phone,
                    o.OrderNumber,
                    o.Date.ToString("yyyy-MM-dd"),
                    o.DueDate?.ToString("yyyy-MM-dd") ?? "",
                    o.Status.ToString(),
                    D(o.Shirt?.Length), D(o.Shirt?.Chest), D(o.Shirt?.Waist),
                    D(o.Shirt?.Hip),    D(o.Shirt?.Shoulder), D(o.Shirt?.Sleeve),
                    D(o.Shirt?.Cuff),   D(o.Shirt?.Collar),
                    D(o.Pant?.Length),  D(o.Pant?.Waist),  D(o.Pant?.Hip),
                    D(o.Pant?.Thigh),   D(o.Pant?.Ankle),  D(o.Pant?.Knee),
                    D(o.Pant?.Seat)
                );
            }
        }

        var dir      = Path.Combine(FileSystem.CacheDirectory, "exports");
        Directory.CreateDirectory(dir);
        var fileName = $"TapeTracker_Orders_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        var filePath = Path.Combine(dir, fileName);

        await MiniExcel.SaveAsAsync(filePath, dt, overwriteFile: true);

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "TapeTracker \u2013 All Orders Export",
            File  = new ShareFile(filePath)
        });
    }
}
