using highlands.Services.ReportServices;
using Microsoft.AspNetCore.Mvc;

namespace highlands.Controllers.Report
{   
    public class ReportController : BaseController
    {
        private readonly ReportService _reportService;
        private readonly ReportEFService _reportEFService;
        private readonly PdfService _pdfService;

        public ReportController(ReportService reportService, PdfService pdfService, ReportEFService reportEFService)
        {
            _reportService = reportService;
            _reportEFService = reportEFService;
            _pdfService = pdfService;
        }
        [HttpGet]
        public async Task<IActionResult> DownloadReport(string type)
        {
            Console.WriteLine($"type truyen vao la: {type}");
            //var data = await _reportService.GenerateReportAsync(type);
            var data = await _reportEFService.GenerateReportAsync(type);
            var pdf = _pdfService.GeneratePdf(data);
            return File(pdf, "application/pdf", $"Report_{type}_{DateTime.Now:yyyyMMdd}.pdf");
        }
    }
}
