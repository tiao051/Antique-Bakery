using ClosedXML.Excel;
using highlands.Models.DTO;

namespace highlands.Services.RabbitMQServices.ExcelServices
{
    public interface IExcelExportService
    {
        Task<string> CreateExcelFile(List<OrderDetailDTO> productPairs);
    }

    public class ExcelExportService : IExcelExportService
    {
        private readonly string _saveDirectory;
        private readonly ILogger<ExcelExportService> _logger;

        public ExcelExportService(IConfiguration configuration, ILogger<ExcelExportService> logger)
        {
            _saveDirectory = configuration.GetValue<string>("ExcelFileSettings:ExcelFileSaveDirectory");
            if (string.IsNullOrWhiteSpace(_saveDirectory))
            {
                throw new ArgumentNullException("ExcelFileSaveDirectory", "Directory path is not configured properly.");
            }
            _logger = logger;
            _logger.LogInformation($"ExcelFileSaveDirectory: {_saveDirectory}");
        }

        public async Task<string> CreateExcelFile(List<OrderDetailDTO> productPairs)
        {
            var filePath = Path.Combine(_saveDirectory, "ProductPairsCSharp.xlsx");

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("ProductPairs");

                    worksheet.Cell(1, 1).Value = "transaction_id";
                    worksheet.Cell(1, 2).Value = "product_detail";

                    int row = 2;
                    foreach (var productPair in productPairs)
                    {
                        var itemNames = productPair.ItemNames.Split(',');

                        foreach (var itemName in itemNames)
                        {
                            worksheet.Cell(row, 1).Value = productPair.OrderId;
                            worksheet.Cell(row, 2).Value = itemName.Trim();
                            row++;
                        }
                    }

                    workbook.SaveAs(filePath);
                }

                _logger.LogInformation($"Excel file created successfully at {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating Excel file: {ex.Message} at {ex.StackTrace}");
                return null;
            }
        }
    }
}
