using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TutorBridgeNepal.Controllers;

[Authorize(Roles = "Tutor")]
public class TutorController : Controller
{
    public IActionResult Dashboard()
    {
        return View();
    }
}