using AutoMapper;
using Enrollments.Bll.Dtos;
using Enrollments.Domain.Models;

namespace Enrollments.Bll.Mapping;

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<OrderItem, OrderItemDto>();
        CreateMap<OrderDetails, OrderDetailsDto>();
        CreateMap<Order, OrderDto>();
        CreateMap<OrderSummary, OrderSummaryDto>();
        CreateMap<OrderStatusHistoryEntry, OrderStatusHistoryDto>();
    }
}
