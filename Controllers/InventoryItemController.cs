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
using CloudinaryDotNet.Actions;
using CloudinaryDotNet;
using Microsoft.AspNetCore.Http;
using Inventar.Utils;

namespace Inventar.Controllers
{
    public class InventoryItemController : Controller
    {
        private readonly ITepihRepository _tepihRepository;
        private readonly ApplicationDbContext _context;
        private readonly IPhotoService _photoService;

        public InventoryItemController(ITepihRepository tepihRepository, ApplicationDbContext context, IPhotoService photoService)
        {
            this._tepihRepository = tepihRepository;
            this._context = context;
            this._photoService = photoService;
        }

        public async Task<IActionResult> Index()
        {
            IEnumerable<Tepih> tepisi = await _tepihRepository.GetAll();
            return View(tepisi);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task< IActionResult> Create(Tepih model)
        {
            if (ModelState.IsValid)
            {
                var time = DateTime.Now.ToString();

                // Generate QR Code for the item
                var qrCodeImageUrl = await GenerateQRCode($"{time}");
                var url = "";

                if (qrCodeImageUrl is OkObjectResult okResult)
                {
                    // Retrieve the actual object from the OkObjectResult
                    var value = okResult.Value as dynamic;

                    // Extract the URL from the object
                    url = value?.url;
                }

                model.QRCodeUrl = url ;
                model.DateTime = time;
                _tepihRepository.Add(model);
                return RedirectToAction("GenerateCloudinaryImagePdf", "Pdf", model);

            }
            return View(model);
        }

        public async Task<IActionResult> GenerateQRCode(string data)
        {
            // Step 1: Generate the QR code using ZXing.Net
            var qrCodeWriter = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = 250,    // Set height
                    Width = 250,     // Set width
                    Margin = 1       // Set margin
                }
            };

            // Generate QR code pixel data
            var pixelData = qrCodeWriter.Write(data);

            // Step 2: Create Bitmap from pixel data
            using (var bitmap = new Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                // Copy the pixel data to the bitmap
                var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }

                // Step 3: Save the bitmap to a memory stream as PNG
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png); // Save as PNG
                    stream.Position = 0; // Reset stream position for upload

                    // Step 4: Upload to Cloudinary
                    var newGuid = $"{Guid.NewGuid()}.png";

                    var uploadResult = await _photoService.UploadToCloudinary(newGuid, stream);

                    if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        // Return the Cloudinary URL of the uploaded QR code
                        return Ok(new { url = uploadResult.SecureUrl.ToString() });
                    }
                    else
                    {
                        return StatusCode(500, "QR code upload to Cloudinary failed");
                    }
                }
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

            if (tepih.QRCodeUrl != null && tepih.QRCodeUrl.Length > 0)
            {
                if (!string.IsNullOrEmpty(tepih.QRCodeUrl))
                {
                    var publicId = CloudinaryHelper.GetPublicIdFromUrlFromFolder(tepih.QRCodeUrl);

                    try
                    {
                        await _photoService.DeletePhotoAsync(publicId);
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Could not delete photo");
                    }
                }
            }
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
