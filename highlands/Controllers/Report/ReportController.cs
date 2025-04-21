using highlands.Services.ReportServices;
using Microsoft.AspNetCore.Mvc;

namespace highlands.Controllers.Report
{   
    public class ReportController : Controller
    {
        private readonly ReportService _reportService;
        private readonly PdfService _pdfService;

        public ReportController(ReportService reportService, PdfService pdfService)
        {
            _reportService = reportService;
            _pdfService = pdfService;
        }
        [HttpGet]
        public async Task<IActionResult> DownloadReport(string type)
        {
            var data = await _reportService.GenerateReportAsync(type);
            var pdf = _pdfService.GeneratePdf(data);
            return File(pdf, "application/pdf", $"Report_{type}_{DateTime.Now:yyyyMMdd}.pdf");
        }
    }
}
