namespace Lernkicker.Web;

/// <summary>Stops the app so that Docker's restart policy starts it again, e.g. to apply a restore.</summary>
public interface IAppRestart
{
    void AfterResponse(HttpResponse response);
}

public sealed class AppRestart(IHostApplicationLifetime lifetime) : IAppRestart
{
    public void AfterResponse(HttpResponse response) =>
        response.OnCompleted(() =>
        {
            lifetime.StopApplication();
            return Task.CompletedTask;
        });
}
