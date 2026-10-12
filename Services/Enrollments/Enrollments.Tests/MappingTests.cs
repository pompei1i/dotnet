using AutoMapper;
using Enrollments.Bll.Dtos;
using Enrollments.Bll.Mapping;
using Enrollments.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Enrollments.Tests;

public class MappingTests
{
    internal static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<StudentProfile>();
            cfg.AddProfile<CourseProfile>();
            cfg.AddProfile<OrderProfile>();
        }, NullLoggerFactory.Instance);

        return configuration.CreateMapper();
    }

    [Fact]
    public void Profiles_configuration_is_valid()
        => CreateMapper().ConfigurationProvider.AssertConfigurationIsValid();

    [Fact]
    public void Order_maps_to_dto_with_details_and_items()
    {
        var order = new Order
        {
            Id = 7,
            StudentId = 1,
            Status = "Submitted",
            TotalAmount = 1000m,
            Details = new OrderDetails { OrderId = 7, BillingName = "Olena", BillingEmail = "o@example.com", PaymentMethod = "Card" }
        };
        order.Items.Add(new OrderItem { OrderId = 7, CourseId = 1, CourseTitle = "C#", UnitPrice = 1200m, Discount = 200m });

        var dto = CreateMapper().Map<OrderDto>(order);

        Assert.Equal(7, dto.Id);
        Assert.Equal("Olena", dto.Details!.BillingName);
        var item = Assert.Single(dto.Items);
        Assert.Equal("C#", item.CourseTitle);
        Assert.Equal(1000m, item.LineTotal);
    }

    [Fact]
    public void Update_dto_maps_row_version_to_model()
    {
        var input = new UpdateStudentDto { Email = "a@example.com", FullName = "Anna", RowVersion = [1, 2, 3, 4, 5, 6, 7, 8] };

        var student = CreateMapper().Map<Student>(input);

        Assert.Equal(input.RowVersion, student.RowVersion);
        Assert.Equal("Anna", student.FullName);
    }
}
