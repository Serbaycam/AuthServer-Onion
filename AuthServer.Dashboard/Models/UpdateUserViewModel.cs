using System.ComponentModel.DataAnnotations;

namespace AuthServer.Dashboard.Models
{
    public class UpdateUserViewModel
    {
        // API "UserId" bekliyor, biz de aynısını veriyoruz
        public Guid UserId { get; set; }

        [Required(ErrorMessage = "Ad alanı zorunludur.")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Soyad alanı zorunludur.")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir email giriniz.")]
        public string Email { get; set; }
    }
}