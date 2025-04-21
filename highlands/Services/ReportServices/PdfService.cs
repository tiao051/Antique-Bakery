using highlands.Models.DTO.ReportDTO;
using QuestPDF.Fluent;

namespace highlands.Services.ReportServices
{
    public class PdfService
    {
        public byte[] GeneratePdf(ReportData data)
        {
            var doc = new ReportDocument(data);
            return doc.GeneratePdf();
        }
    }
}
