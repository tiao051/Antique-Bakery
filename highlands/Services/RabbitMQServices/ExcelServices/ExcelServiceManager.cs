using highlands.Models.DTO;

namespace highlands.Services.RabbitMQServices.ExcelServices
{
    public class ExcelServiceManager
    {
        private readonly IExcelExportService _excelExportService;
        private readonly IExcelQueuePublisherService _excelQueuePublisherService;

        public ExcelServiceManager(
            IExcelExportService excelExportService,
            IExcelQueuePublisherService excelQueuePublisherService) 
        {
            _excelExportService = excelExportService;
            _excelQueuePublisherService = excelQueuePublisherService;
        }

        public async Task<string> CreateExcelFileAsync(List<OrderDetailDTO> productPairs)
        {
            return await _excelExportService.CreateExcelFile(productPairs);
        }

        public async Task PublishFilePathAsync(string filePath)
        {
            await _excelQueuePublisherService.PublishExcelFilePathAsync(filePath);
        }
    }
}
