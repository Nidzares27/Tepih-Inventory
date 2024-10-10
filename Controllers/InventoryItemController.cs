using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using ZXing.QrCode;
using ZXing;
using Inventar.Models;
using Inventar.Interfaces;
using System.Collections;
using Inventar.Data;
using Microsoft.AspNetCore.Authorization;
using System.Data;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using Inventar.ViewModels;

namespace Inventar.Controllers
{
    public class InventoryItemController : Controller
    {
        //private static List<Tepih> inventoryItems = new List<Tepih>();
        private readonly ITepihRepository _tepihRepository;
        private readonly ApplicationDbContext _context;

        public InventoryItemController(ITepihRepository tepihRepository, ApplicationDbContext context)
        {
            this._tepihRepository = tepihRepository;
            this._context = context;
        }

        public async Task<IActionResult> Index()
        {
            IEnumerable<Tepih> tepisi = await _tepihRepository.GetAll();
            return View(tepisi);
        }

        // Action to create a new Inventory item
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(Tepih model)
        {
            if (ModelState.IsValid)
            {

                //model.Id = inventoryItems.Count > 0 ? inventoryItems.Max(i => i.Id) + 1 : 1;
                var time = DateTime.Now.ToString();
                // Generate QR Code for the item
                //var qrCodeImageUrl = GenerateQRCode($"{model.Id}-{model.Name}");
                var qrCodeImageUrl = GenerateQRCode($"{time}");
                //var qrCodeImageUrl = GenerateQRCode($"{DateTime.Now}");

                model.QRCodeUrl = qrCodeImageUrl;
                model.DateTime = time;
                //inventoryItems.Add(model);
                _tepihRepository.Add(model);
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // QR Code Generation Method
        private string GenerateQRCode(string content)
        {
            // Generate QR Code using ZXing.Net
            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = 200,
                    Width = 200,
                    Margin = 1
                }
            };

            var pixelData = writer.Write(content);
            using (var bitmap = new Bitmap(pixelData.Width, pixelData.Height))
            {
                for (int y = 0; y < pixelData.Height; y++)
                {
                    for (int x = 0; x < pixelData.Width; x++)
                    {
                        Color pixelColor = pixelData.Pixels[(y * pixelData.Width + x) * 4] == 0 ? Color.Black : Color.White;
                        bitmap.SetPixel(x, y, pixelColor);
                    }
                }

                // Save bitmap as PNG
                var fileName = $"{Guid.NewGuid()}.png";
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", fileName);
                bitmap.Save(filePath);

                // Return relative path for use in view
                return $"/images/{fileName}";
            }
        }

        [HttpGet]
        public async Task<IActionResult> QRCodeScanning(int id)
        {
            //Find the inventory item by its id
           var item = await _tepihRepository.GetByIdAsync(id);

            if (item == null)
            {
                return NotFound();
            }

            // Pass the QR Code URL to the view
            ViewBag.QRCodeUrl = item.QRCodeUrl;

            return View(item);
        }

        [HttpGet]
        public async Task<IActionResult> QRCodeScanning2()
        {
            return View("QRCodeScanning");
        }

        public IActionResult ProcessQRCode(string data)
        {
            // Search for the inventory item based on the QR code data
            var item = _context.Tepisi.FirstOrDefault(i => i.DateTime == data);

            if (item != null)
            {
                // Display the item details or redirect to a detail page
                return View("QRCodeScanning",item);
            }

            // If not found, show an error or handle accordingly
            ViewBag.Error = "Item not found";
            return View("Error");
        }

        public async Task<IActionResult> Update (int id)
        {
            var item = await _tepihRepository.GetByIdAsync(id);

            if (item == null)
            {
                return NotFound();
            }

            item.Quantity--;
            _tepihRepository.Update(item);

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Delete(int id)
        {
            Tepih tepih = await _tepihRepository.GetByIdAsyncNoTracking(id);
            return View(tepih);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteTepih(int id)
        {
            Tepih tepih = await _tepihRepository.GetByIdAsync(id);

            _tepihRepository.Delete(tepih);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(int id)
        {
            Tepih tepih = await _tepihRepository.GetByIdAsyncNoTracking(id);
            if (tepih == null) return View("Error");
            var tepihVM = new EditTepihViewModel
            {
                Name = tepih.Name,
                Description = tepih.Description,
                DateTime = tepih.DateTime,
                Quantity = tepih.Quantity,
                QRCodeUrl = tepih.QRCodeUrl
            };

            return View(tepihVM);
        }
        [HttpPost]
        public async Task<IActionResult> Edit(int id, EditTepihViewModel tepihVM)
        {

            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Editovanje tepiha nije uspjelo");
                return View("Edit", tepihVM);
            }

            var putnik = await _tepihRepository.GetByIdAsyncNoTracking(id);

            if (putnik != null)
            {
                var tepihEdit = new Tepih
                {
                    Id = id,
                    Name = tepihVM.Name,
                    Description = tepihVM.Description,
                    DateTime = tepihVM.DateTime,
                    Quantity = tepihVM.Quantity,
                    QRCodeUrl = tepihVM.QRCodeUrl
                };

                _tepihRepository.Update(tepihEdit);

                return RedirectToAction("Index");
            }
            else
            {
                return View(tepihVM);
            }
        }

    }
}
