using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleApi.StatelessBackgroundServices
{

    public class WorkerOptions<T> where T : IStatelessWorker
    {
        public TimeSpan Pause { get; set; }

        public TimeSpan? StartDelay { get; set; }
    }





    public class StatelessWorkerManager
    {
        private readonly IServiceProvider _services;

        public StatelessWorkerManager(IServiceProvider services)
        {
            _services = services;
        }

        public StatelessBackgroundService<T> GetByWorker<T>() where T : IStatelessWorker => _services.GetService<IEnumerable<IHostedService>>()?.GetClass<StatelessBackgroundService<T>>();
    }





    public static class ServiceCollectionHostedServiceExtensions
    {
        public static IServiceCollection AddStatelessWorker<T>(this IServiceCollection services, Action<WorkerOptions<T>> setupAction) where T : class, IStatelessWorker
        {
            if (setupAction == null)
            {
                throw new ArgumentNullException(nameof(setupAction));
            }

            services.AddTransient<T>();
            services.TryAddTransient<StatelessWorkerManager>();
            services.Configure(setupAction);

            return services.AddSingleton<IHostedService, StatelessBackgroundService<T>>();
        }


        public static T GetInterface<T>(this IEnumerable<IHostedService> services) where T : class
        {
            if (!(services.FirstOrDefault(x =>
                    x.GetType() is TypeInfo &&
                    ((TypeInfo)x.GetType()).ImplementedInterfaces.Contains(typeof(T)))
                is T @interface))
                return null;

            return @interface;
        }

        public static T GetClass<T>(this IEnumerable<IHostedService> services) where T : class
        {
            if (!(services.FirstOrDefault(x => x.GetType() == typeof(T)) is T @class))
                return null;

            return @class;
        }
    }



    public interface IStatelessWorker
    {
        public Task DoWork();
    }



    public sealed class StatelessBackgroundService<T> : BackgroundService where T : IStatelessWorker
    {
        private IServiceScopeFactory ScopeFactory { get; }

        private string SvcName => typeof(T).Name;

        private TimeSpan Pause { get; set; }

        private TimeSpan? StartDelay { get; }

        public CancellationTokenSource ForceToken { get; private set; } = new CancellationTokenSource();


        public StatelessBackgroundService(IServiceScopeFactory serviceScopeFactory, IOptions<WorkerOptions<T>> opts)
        {
            ScopeFactory = serviceScopeFactory;
            Pause = opts.Value.Pause;
            StartDelay = opts.Value.StartDelay;
        }

        public void ChangeTimerDelay(int secs) => Pause = TimeSpan.FromSeconds(secs);

        public void ChangeTimerDelay(TimeSpan pause) => Pause = pause;


        private async Task DoWork()
        {
            var scope = ScopeFactory.CreateScope();

            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(SvcName);

            logger.LogDebug($"{SvcName} start");

            try
            {
                var worker = scope.ServiceProvider.GetRequiredService<T>();
                await worker.DoWork();
            }

            catch (Exception e)
            {
                logger.LogError($"{SvcName} error: {e.Message}");
            }

            finally
            {
                scope?.Dispose();
            }

            logger.LogDebug($"{SvcName} end");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ChangeTimerDelay(Pause);

            if (StartDelay != null)
            {
                await Task.Delay(StartDelay.Value, stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                ForceToken = new CancellationTokenSource();

                await DoWork();

                await Task.WhenAny(Task.Delay(Pause, stoppingToken), Task.Delay(-1, ForceToken.Token));
#warning extra code below???
                ForceToken = new CancellationTokenSource();
            }
        }
    }
}
