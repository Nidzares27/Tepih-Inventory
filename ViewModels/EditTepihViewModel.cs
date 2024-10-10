using System.ComponentModel.DataAnnotations;

namespace Inventar.ViewModels
{
    public class EditTepihViewModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? DateTime { get; set; }
        public int Quantity { get; set; }
        public string? QRCodeUrl { get; set; }
    }
}