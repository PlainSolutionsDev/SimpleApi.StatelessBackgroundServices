# SimpleApi.StatelessBackgroundServices

SimpleApi.StatelessBackgroundServices is a library for creating and managing stateless background services in ASP.NET Core.

Stateless background services are services that perform some work without saving state between calls. They are useful for periodic tasks, such as clearing cache, sending notifications or processing message queues.

## How to use

To use the library, you need to do the following:

- Create a class that implements the IStatelessWorker interface. This class should contain the logic of your background work in the DoWork method.
- Register your stateless background service in the dependency injection container using the AddStatelessWorker extension method. You can configure the options of your service, such as pause and start delay, using the setupAction parameter.
- Get access to your stateless background service through the StatelessWorkerManager class. You can use this class to change the timer delay or force the execution of your service.

## Example

Here is an example of a stateless background service that sends an email every 10 minutes:

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleApi.StatelessBackgroundServices;

// Define a class that implements IStatelessWorker
public class EmailSender : IStatelessWorker
{
    private readonly IEmailService _emailService;

    // Inject any dependencies you need
    public EmailSender(IEmailService emailService)
    {
        _emailService = emailService;
    }

    // Implement the DoWork method with your background logic
    public async Task DoWork()
    {
        await _emailService.SendEmailAsync("Hello world!");
    }
}

// Register your stateless background service in Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Add any dependencies you need
    services.AddTransient<IEmailService, EmailService>();

    // Add your stateless background service with options
    services.AddStatelessWorker<EmailSender>(opts =>
    {
        opts.Pause = TimeSpan.FromMinutes(10); // Set the pause between calls
        opts.StartDelay = TimeSpan.FromSeconds(30); // Set the delay before the first call
    });
}

// Get access to your stateless background service in a controller or anywhere else
public class HomeController : Controller
{
    private readonly StatelessWorkerManager _workerManager;

    // Inject the StatelessWorkerManager
    public HomeController(StatelessWorkerManager workerManager)
    {
        _workerManager = workerManager;
    }

    public IActionResult Index()
    {
        // Get your stateless background service by worker type
        var emailSender = _workerManager.GetByWorker<EmailSender>();

        // Change the timer delay if you want
        emailSender.ChangeTimerDelay(TimeSpan.FromMinutes(5));

        // Force the execution of your service if you want
        emailSender.ForceToken.Cancel();

        return View();
    }
}
```
