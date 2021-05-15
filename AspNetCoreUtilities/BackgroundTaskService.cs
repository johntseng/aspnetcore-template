using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RollbarDotNet;

namespace AspNetCoreUtilities
{
    /// <summary>
    /// Runs a service in the background. Upon server start, run DoIt() over and over again until the server is stopped.
    /// </summary>
    public abstract class BackgroundTaskService
    {
        private readonly CancellationTokenSource _cancellationTokenSource =
            new CancellationTokenSource();

        // A token to indicate when the server has stopped and execution stop. Check this token to cancel a task midway.
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
                            try
                            {
                                DoIt(scope.ServiceProvider);
                            }
                            catch (OperationCanceledException)
                            {
                                return;
                            }
                            catch (Exception e)
                            {
                                try
                                {
                                    scope.ServiceProvider.GetRequiredService<Rollbar>()
                                    .SendException(e).Wait(CancellationToken);
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
                    }
                    catch
                    {
                        // ignored
                    }
                }
            });
        }

        // Register this service to start and stop with the application.
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

        // Sleeps for the specified TimeSpan, and throw if cancellation is requested.
        // Use this instead of Thread.Sleep so that the thread will be woken up when the application is stopping.
        protected void SleepAndThrowIfCancellationRequested(TimeSpan timeSpan)
        {
            CancellationToken.WaitHandle.WaitOne(timeSpan);
            CancellationToken.ThrowIfCancellationRequested();
        }

        // The action to repeatedly perform.
        protected abstract void DoIt(IServiceProvider serviceProvider);
        // The time to sleep if there is an exception.
        protected virtual TimeSpan TimeToWaitAfterException => TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Extension methods for BackgroundTaskService.
    /// </summary>
    public static class BackgroundTaskServiceExtensions
    {
        // Registers a BackgroundTaskService with the application.
        public static void UseBackgroundTaskService<T>(this IApplicationBuilder builder)
            where T : BackgroundTaskService =>
            builder.ApplicationServices.GetRequiredService<T>().Register(
                builder.ApplicationServices.GetRequiredService<IApplicationLifetime>(),
                builder.ApplicationServices.GetRequiredService<IServiceScopeFactory>());
    }
}
