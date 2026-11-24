using Enrollments.Bll.Dtos;
using Enrollments.Bll.Services;
using Enrollments.Api.Metrics;
using Enrollments.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Enrollments.Api.Controllers;

/// <summary>
/// Тонкий контролер: приймає запит, викликає сервіс і повертає HTTP-статус.
/// Бізнес-правил і SQL тут немає; винятки перетворює DomainExceptionHandler.
/// Лаб. 07: кожна операція записує тривалість (успіх і відмова) в orders.operation.duration,
/// а успішна — ще й бізнес-лічильник (orders.created, orders.status_changed...).
/// </summary>
[ApiController]
[Route("api/orders")]
public class OrdersController(IOrderService orders, OrderMetrics metrics) : ControllerBase
{
    /// <summary>Замовлення з реквізитами та позиціями.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(long id, CancellationToken ct)
        => Ok(await metrics.MeasureAsync("get", () => orders.GetByIdAsync(id, ct)));

    /// <summary>Історія зміни статусів замовлення.</summary>
    [HttpGet("{id:long}/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OrderStatusHistoryDto>>> GetHistory(long id, CancellationToken ct)
        => Ok(await orders.GetHistoryAsync(id, ct));

    /// <summary>Оформлює замовлення з позиціями однією транзакцією.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderDto input, CancellationToken ct)
    {
        var created = await metrics.MeasureAsync("create", () => orders.CreateAsync(input, ct));
        metrics.OrderCreated(input.PaymentMethod, created.TotalAmount);   // лише після успішного збереження
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Додає курс до замовлення у статусі Submitted.</summary>
    [HttpPost("{id:long}/items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> AddItem(long id, CreateOrderItemDto input, CancellationToken ct)
    {
        var order = await metrics.MeasureAsync("add_item", () => orders.AddItemAsync(id, input, ct));
        metrics.ItemAdded();
        return Ok(order);
    }

    /// <summary>Відправляє замовлення на оплату: Submitted → AwaitingPayment.</summary>
    [HttpPost("{id:long}/checkout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Checkout(long id, CancellationToken ct)
    {
        await ChangeStatusAsync(id, OrderStatuses.AwaitingPayment, ct);
        return NoContent();
    }

    /// <summary>Позначає замовлення оплаченим: AwaitingPayment → Paid.</summary>
    [HttpPost("{id:long}/pay")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Pay(long id, CancellationToken ct)
    {
        await ChangeStatusAsync(id, OrderStatuses.Paid, ct);
        return NoContent();
    }

    /// <summary>Відкриває доступ до курсів: Paid → AccessGranted.</summary>
    [HttpPost("{id:long}/grant-access")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GrantAccess(long id, CancellationToken ct)
    {
        await ChangeStatusAsync(id, OrderStatuses.AccessGranted, ct);
        return NoContent();
    }

    /// <summary>Скасовує замовлення; можливо лише до оплати.</summary>
    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        await ChangeStatusAsync(id, OrderStatuses.Cancelled, ct);
        return NoContent();
    }

    /// <summary>Повертає кошти: Paid → Refunded.</summary>
    [HttpPost("{id:long}/refund")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Refund(long id, CancellationToken ct)
    {
        await ChangeStatusAsync(id, OrderStatuses.Refunded, ct);
        return NoContent();
    }

    /// <summary>М'яко видаляє чернетку або скасоване замовлення.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await metrics.MeasureAsync("delete", () => orders.DeleteAsync(id, ct));
        return NoContent();
    }

    private async Task ChangeStatusAsync(long id, string newStatus, CancellationToken ct)
    {
        await metrics.MeasureAsync("change_status", () => orders.ChangeStatusAsync(id, newStatus, ct));
        metrics.StatusChanged(newStatus);
    }
}
