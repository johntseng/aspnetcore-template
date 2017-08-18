using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCoreUtilities
{
    public abstract class BackgroundTaskService
    {
        private readonly CancellationTokenSource _cancellationTokenSource =
            new CancellationTokenSource();

        protected CancellationToken CancellationToken => _cancellationTokenSource.Token;
        private Thread _thread;

        private void CreateThread(IServiceScopeFactory serviceScopeFactory)
        {
            _thread = new Thread(() =>
            {
                while (!CancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        using (var scope = serviceScopeFactory.CreateScope())
                        {
                            DoIt(scope.ServiceProvider).Wait();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception e)
                    {
                        try
                        {
                            Console.WriteLine(e);
                        }
                        catch (Exception)
                        {
                        }
                        try
                        {
                            SleepAndThrowIfCancellationRequested(TimeToWaitAfterException);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                    }
                }
            });
        }

        internal void Register(IApplicationLifetime lifetime, IServiceScopeFactory serviceScopeFactory)
        {
            CreateThread(serviceScopeFactory);
            lifetime.ApplicationStarted.Register(() =>
            {
                _thread.Start();
            });
            lifetime.ApplicationStopping.Register(() =>
            {
                _cancellationTokenSource.Cancel();
                _thread.Join();
            });
        }

        protected void SleepAndThrowIfCancellationRequested(TimeSpan timeSpan)
        {
            CancellationToken.WaitHandle.WaitOne(timeSpan);
            CancellationToken.ThrowIfCancellationRequested();
        }

        protected abstract Task DoIt(IServiceProvider serviceProvider);
        protected abstract TimeSpan TimeToWaitAfterException { get; }
    }

    public static class BackgroundTaskServiceExtensions
    {
        public static void UseBackgroundTaskService<T>(this IApplicationBuilder builder)
            where T : BackgroundTaskService =>
            builder.ApplicationServices.GetRequiredService<T>().Register(
                builder.ApplicationServices.GetRequiredService<IApplicationLifetime>(),
                builder.ApplicationServices.GetRequiredService<IServiceScopeFactory>());
    }
}
