using System.ComponentModel.DataAnnotations;

namespace AuthServer.Web
{
    public class CreateTestModuleRequest
    {
        [Required(ErrorMessage = "Test adı zorunludur.")]
        public string Name { get; set; } // Örn: Yıkama Haslığı

        [Required(ErrorMessage = "Kod zorunludur.")]
        public string Code { get; set; } // Örn: ISO-105

        public string Description { get; set; }
    }
}