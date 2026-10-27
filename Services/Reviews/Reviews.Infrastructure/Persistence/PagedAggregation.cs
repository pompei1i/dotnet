using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Reviews.Application.Common.Models;

namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// Сторінка списку одним aggregation pipeline:
///   $match (фільтр) -> $facet { items: [$sort, $skip, $limit, $project], total: [$count] }.
/// Фільтр, сортування, пагінація й проєкція виконуються в MongoDB; клієнт отримує один документ
/// з потрібними полями поточної сторінки та загальною кількістю.
/// </summary>
internal static class PagedAggregation
{
    public static BsonDocument[] BuildPipeline<TDocument>(
        FilterDefinition<TDocument> filter,
        SortDefinition<TDocument> sort,
        BsonDocument projection,
        int page,
        int pageSize)
    {
        var args = RenderArgs<TDocument>();
        return
        [
            // $text, якщо є у фільтрі, мусить стояти в першому $match конвеєра — так і є
            new BsonDocument("$match", filter.Render(args)),
            new BsonDocument("$facet", new BsonDocument
            {
                {
                    "items", new BsonArray
                    {
                        new BsonDocument("$sort", sort.Render(args)),
                        new BsonDocument("$skip", (page - 1) * pageSize),
                        new BsonDocument("$limit", pageSize),
                        new BsonDocument("$project", projection)
                    }
                },
                { "total", new BsonArray { new BsonDocument("$count", "count") } }
            })
        ];
    }

    public static async Task<PagedResult<TDto>> ExecuteAsync<TDocument, TDto>(
        IMongoCollection<TDocument> collection,
        BsonDocument[] pipeline,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var result = await collection
            .Aggregate(PipelineDefinition<TDocument, BsonDocument>.Create(pipeline), cancellationToken: ct)
            .FirstOrDefaultAsync(ct);

        // Проєкція $project уже має форму DTO — лишається десеріалізувати її в record
        var items = result?["items"].AsBsonArray
            .Select(item => BsonSerializer.Deserialize<TDto>(item.AsBsonDocument))
            .ToList() ?? [];
        var total = result?["total"].AsBsonArray.FirstOrDefault()?["count"].ToInt64() ?? 0;

        return new PagedResult<TDto>(items, total, page, pageSize);
    }

    public static RenderArgs<TDocument> RenderArgs<TDocument>()
        => new(BsonSerializer.LookupSerializer<TDocument>(), BsonSerializer.SerializerRegistry);

    /// <summary>{ $ifNull: [ "$field", fallback ] } — поле завжди є в результаті, навіть коли його немає в документі.</summary>
    public static BsonDocument IfNull(string fieldPath, BsonValue fallback)
        => new("$ifNull", new BsonArray { fieldPath, fallback });
}
