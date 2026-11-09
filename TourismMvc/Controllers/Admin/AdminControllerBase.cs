using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TourismMvc.Controllers.Admin;

[Authorize(Roles = "ADMIN")]
public abstract class AdminControllerBase : Controller;
