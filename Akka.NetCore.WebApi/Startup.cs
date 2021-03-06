using Akka.Actor;
using Akka.NetCore.WebApi.Actors;
using Akka.NetCore.WebApi.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;

namespace Akka.NetCore.WebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //var actorSystem = ActorSystem.Create("calculator-actor-system");
            //services.AddSingleton(typeof(ActorSystem), (serviceProvider) => actorSystem);

            services.AddSingleton(provider =>
            {
                var serviceScopeFactory = provider.GetService<IServiceScopeFactory>();
                var actorSystem = ActorSystem.Create("my-actor-system");
                actorSystem.AddServiceScopeFactory(serviceScopeFactory);

                return actorSystem;
            });

            services.AddSingleton<BooksManagerActorProvider>(provider =>
            {
                var actorSystem = provider.GetService<ActorSystem>();
                var booksManagerActor = actorSystem.ActorOf(Props.Create(() => new BooksManagerActor()));
                return () => booksManagerActor;
            });

            services.AddDbContext<BookDbContext>(options =>
                options.UseInMemoryDatabase("ActorModelInMemoryDB")
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Akka.NetCore.WebApi", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime, IServiceProvider provider)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Akka.NetCore.WebApi v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            lifetime.ApplicationStarted.Register(() =>
            {
                app.ApplicationServices.GetService<ActorSystem>(); // start Akka.Net
            });

            lifetime.ApplicationStopping.Register(() =>
            {
                app.ApplicationServices.GetService<ActorSystem>().Terminate().Wait();
            });

            var bookDbContext = provider.GetService<BookDbContext>();
            AddDummyDataToInMemoryDatabase(bookDbContext);
        }

        private static void AddDummyDataToInMemoryDatabase(BookDbContext bookDbContext)
        {
            bookDbContext.Books.Add(new Domain.Book
            {
                Id = Guid.NewGuid(),
                Title = "Book 1",
                Author = "Author 1",
                InventoryAmount = 12,
                Cost = 2.99M
            });

            bookDbContext.Books.Add(new Domain.Book
            {
                Id = Guid.NewGuid(),
                Title = "Book 2",
                Author = "Author 2",
                InventoryAmount = 2,
                Cost = 4.99M
            });

            bookDbContext.Books.Add(new Domain.Book
            {
                Id = Guid.NewGuid(),
                Title = "Book 3",
                Author = "Author 3",
                InventoryAmount = 10,
                Cost = 7.99M
            });

            bookDbContext.SaveChangesAsync().Wait();
        }
    }
}
