using Chorbar.Templates;
using Chorbar.Utils;
using Microsoft.AspNetCore.Mvc;

namespace Chorbar.Controllers;

[Route("/")]
public class HomeController : Controller
{
    [HttpGet("")]
    public IResult Index() => new PageResult(new LandingPage());
}
