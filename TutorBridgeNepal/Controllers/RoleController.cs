using Microsoft.AspNetCore.Mvc;

namespace TutorBridgeNepal.Controllers;

public class RoleController : Controller
{
    public IActionResult Select()
    {
        return View();
    }
}