using Yarp.ReverseProxy.Configuration;

namespace ReverseProxy;

public class ReverseProxyOptions
{
    private const string QuestionAppCluster = nameof(QuestionAppCluster);
    private const string QuestionAppRoute = nameof(QuestionAppRoute);

    public string QuestionsAppUrl { get; set; } = "";

    public ClusterConfig[] GetClusters()
        => new[]
        {
            new ClusterConfig
            {
                ClusterId = QuestionAppCluster,
                Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    {"Site1", new DestinationConfig() { Address = QuestionsAppUrl } }
                }
            }
        };

    public RouteConfig[] GetRoutes()
     => new[]
     {
         new RouteConfig {
                ClusterId = QuestionAppCluster,
                RouteId = QuestionAppRoute,
                Match = new()
                {
                    Path = "{**catch-all}",
                }
         }
     };
}
