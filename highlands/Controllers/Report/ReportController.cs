using highlands.Services.ReportServices;
using Microsoft.AspNetCore.Mvc;

namespace highlands.Controllers.Report
{   
    public class ReportController : Controller
    {
        private readonly ReportService _reportService;
        private readonly PdfService _pdfService;

        public ReportController()
        {
            _reportService = new ReportService();
            _pdfService = new PdfService();
        }
        [HttpGet]
        public IActionResult DownloadReport(string type)
        {
            var data = _reportService.GenerateReport(type);
            var pdf = _pdfService.GeneratePdf(data);
            return File(pdf, "application/pdf", $"Report_{type}_{DateTime.Now:yyyyMMdd}.pdf");
        }
    }
}
