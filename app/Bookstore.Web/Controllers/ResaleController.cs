using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Bookstore.Web.ViewModel.Resale;
using Bookstore.Web.Helpers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.ReferenceData;

namespace Bookstore.Web.Controllers
{
    public class ResaleController : Controller
    {
        private readonly IReferenceDataService referenceDataService;
        private readonly IOfferService offerService;

        public ResaleController(IReferenceDataService referenceDataService, IOfferService offerService)
        {
            this.referenceDataService = referenceDataService;
            this.offerService = offerService;
        }

        public async Task<IActionResult> Index()
        {
            var offers = await offerService.GetOffersAsync(User.GetSub());

            return View(new ResaleIndexViewModel(offers));
        }

        public async Task<IActionResult> Create()
        {
            var referenceDataDtos = await referenceDataService.GetAllReferenceDataAsync();

            return View(new ResaleCreateViewModel(referenceDataDtos));
        }

        [HttpPost]
        public async Task<IActionResult> Create(ResaleCreateViewModel resaleViewModel)
        {
            // The Create view binds Model.Publishers/Conditions/BookTypes/Genres, so the
            // reference data select lists must be repopulated before redisplaying the form -
            // returning a bare View() left the model null and threw on render.
            if (!ModelState.IsValid) return await InvalidCreateView(resaleViewModel);

            var dto = new CreateOfferDto(
                User.GetSub(), 
                resaleViewModel.BookName!, 
                resaleViewModel.Author!, 
                resaleViewModel.ISBN!, 
                resaleViewModel.SelectedBookTypeId, 
                resaleViewModel.SelectedConditionId, 
                resaleViewModel.SelectedGenreId, 
                resaleViewModel.SelectedPublisherId, 
                resaleViewModel.BookPrice);

            await offerService.CreateOfferAsync(dto);

            return RedirectToAction(nameof(Index));
        }

        // Re-hydrates the reference-data select lists and redisplays the Create form, mirroring
        // the pattern used by Areas/Admin InventoryController.InvalidCreateUpdateView.
        private async Task<IActionResult> InvalidCreateView(ResaleCreateViewModel model)
        {
            var referenceDataDtos = await referenceDataService.GetAllReferenceDataAsync();

            var referenceData = new ResaleCreateViewModel(referenceDataDtos);

            model.BookTypes = referenceData.BookTypes;
            model.Publishers = referenceData.Publishers;
            model.Genres = referenceData.Genres;
            model.Conditions = referenceData.Conditions;

            return View(model);
        }
    }
}