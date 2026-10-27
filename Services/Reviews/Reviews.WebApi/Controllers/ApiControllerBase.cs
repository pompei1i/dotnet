using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Reviews.WebApi.Controllers;

/// <summary>
/// Базовий контролер: доступ до ISender (MediatR) і IMapper. Контролери лише перетворюють
/// HTTP-запит на команду чи запит і передають його в MediatR — без логіки й без доступу до бази.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    private IMapper? _mapper;

    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected IMapper Mapper => _mapper ??= HttpContext.RequestServices.GetRequiredService<IMapper>();
}
