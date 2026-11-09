using Yarp.ReverseProxy.Model;

namespace Platform.ApiGateway.Logging;

/// <summary>
/// Крок конвеєра YARP після load balancing: на цей момент відомо, за яким маршрутом збігся запит,
/// в який кластер він іде і який саме екземпляр сервісу обрано. Метадані маршруту (Team, Version)
/// з appsettings.json теж потрапляють у лог.
/// </summary>
public sealed class RoutingDecisionLogger(RequestDelegate next, ILogger<RoutingDecisionLogger> logger)
{
    public Task InvokeAsync(HttpContext context)
    {
        var proxy = context.GetReverseProxyFeature();
        var route = proxy.Route.Config;
        var destinations = proxy.AvailableDestinations;

        if (destinations.Count == 0)
        {
            logger.LogWarning("Route {RouteId} matched {Method} {Path}, but cluster {ClusterId} has no available destinations",
                route.RouteId, context.Request.Method, context.Request.Path.Value, proxy.Cluster.Config.ClusterId);
        }
        else
        {
            logger.LogInformation(
                "Routing {Method} {Path} via route {RouteId} (team {Team}, version {RouteVersion}) to cluster {ClusterId}, destination {Destination}",
                context.Request.Method, context.Request.Path.Value, route.RouteId,
                route.Metadata?.GetValueOrDefault("Team"), route.Metadata?.GetValueOrDefault("Version"),
                proxy.Cluster.Config.ClusterId, destinations[0].Model.Config.Address);
        }

        return next(context);
    }
}
