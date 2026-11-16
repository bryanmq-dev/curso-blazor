using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sharpness_sharp.Components.Account;
using sharpness_sharp.Data;
using sharpness_sharp.Data.Entities;
using sharpness_sharp.Data.Enums;

namespace sharpness_sharp.Config
{
    public static class SharpnessAuth
    {
        /// <summary>
        /// Todo lo que el template `-au Individual` dejaba suelto en Program.cs:
        /// el DbContext de Identity, el esquema de cookies, el AuthenticationStateProvider,
        /// los scoped del área de Account y el UserManager/SignInManager.
        ///
        /// UseAuth es la entrada: llama a UseDB() por dentro con conAuth: true, así la base
        /// queda configurada una sola vez y con el contexto que corresponde.
        ///
        ///     builder.Services.UseAuth(builder.Configuration);   // con login
        ///     builder.Services.UseDB(builder.Configuration);     // sin login
        /// </summary>
        public static IServiceCollection UseAuth(this IServiceCollection services, IConfiguration configuration)
            => services.UseAuth(DatabaseParams.FromConfiguration(configuration));

        /// <summary>Lo mismo, con los parámetros de la base escritos a mano.</summary>
        /// <remarks>
        /// AddRoles va ANTES de AddEntityFrameworkStores: el store de roles sólo se registra
        /// si el IdentityBuilder ya tiene seteado el tipo de rol. Invertir ese orden deja la
        /// aplicación sin RoleManager y hace que [Authorize(Roles = "...")] nunca autorice,
        /// porque el claim de rol no entra en la cookie.
        /// </remarks>
        public static IServiceCollection UseAuth(this IServiceCollection services, DatabaseParams db)
        {
            services.UseDB(db, conAuth: true);

            services.AddCascadingAuthenticationState();

            services.AddScoped<IdentityRedirectManager>();
            services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
                })
                .AddIdentityCookies();

            services.AddIdentityCore<ApplicationUser>(options =>
                {
                    options.SignIn.RequireConfirmedAccount = false;
                    options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationAuthDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

            services.ConfigureApplicationCookie(opciones =>
            {
                opciones.Cookie.HttpOnly = true;
                opciones.Events.OnRedirectToLogin = contexto => RedirigirOResponder(contexto, StatusCodes.Status401Unauthorized);
                opciones.Events.OnRedirectToAccessDenied = contexto => RedirigirOResponder(contexto, StatusCodes.Status403Forbidden);
            });

            return services;
        }

        /// <summary>
        /// Deja la base lista para usar: aplica las migraciones pendientes, crea los roles
        /// de "Auth:Roles" y siembra el usuario administrador.
        ///
        ///     await app.SeedAuth();
        ///
        /// Es idempotente: correrlo en cada arranque no duplica nada.
        /// </summary>
        /// <remarks>
        /// El administrador se crea con EmailConfirmed en true a propósito: UseAuth deja
        /// RequireConfirmedAccount en true y no hay mail que confirmar, así que sin esa
        /// bandera el usuario sembrado no podría iniciar sesión.
        ///
        /// Migrar al arrancar alcanza para un despliegue de una sola instancia. Con varias
        /// réplicas levantando a la vez conviene sacar el MigrateAsync y aplicar las
        /// migraciones en un paso aparte del deploy.
        /// </remarks>
        public static async Task<WebApplication> SeedAuth(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var sp = scope.ServiceProvider;
            var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SharpnessAuth));

            var db = sp.GetService<ApplicationAuthDbContext>()
                ?? throw new InvalidOperationException(
                    "SeedAuth() necesita UseAuth(). Si la aplicación va sin login, sacá la llamada de Program.cs.");

            await db.Database.MigrateAsync();

            var auth = AuthParams.FromConfiguration(app.Configuration);
            var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

            foreach (var rol in auth.TodosLosRoles())
            {
                if (await roleManager.RoleExistsAsync(rol)) continue;

                var creado = await roleManager.CreateAsync(new IdentityRole(rol));
                if (!creado.Succeeded)
                    throw new InvalidOperationException($"SeedAuth: no se pudo crear el rol '{rol}'. {Errores(creado)}");

                log.LogInformation("SeedAuth: rol '{Rol}' creado.", rol);
            }

            if (string.IsNullOrWhiteSpace(auth.AdminEmail) || string.IsNullOrWhiteSpace(auth.AdminPassword))
            {
                log.LogWarning(
                    "SeedAuth: roles listos, pero no se sembró el administrador porque falta Auth:AdminEmail " +
                    "o Auth:AdminPassword. Definilos con variables de entorno (Auth__AdminPassword) o user-secrets.");
                return app;
            }

            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = await userManager.FindByEmailAsync(auth.AdminEmail);

            if (admin is null)
            {
                admin = new ApplicationUser
                {
                    UserName = auth.AdminEmail,
                    Email = auth.AdminEmail,
                    EmailConfirmed = true,
                };

                var creado = await userManager.CreateAsync(admin, auth.AdminPassword);
                if (!creado.Succeeded)
                    throw new InvalidOperationException(
                        $"SeedAuth: no se pudo crear el administrador '{auth.AdminEmail}'. {Errores(creado)}");

                log.LogInformation("SeedAuth: administrador '{Email}' creado.", auth.AdminEmail);
            }

            if (!await userManager.IsInRoleAsync(admin, auth.AdminRole))
            {
                var asignado = await userManager.AddToRoleAsync(admin, auth.AdminRole);
                if (!asignado.Succeeded)
                    throw new InvalidOperationException(
                        $"SeedAuth: no se pudo asignar el rol '{auth.AdminRole}'. {Errores(asignado)}");

                log.LogInformation("SeedAuth: '{Email}' agregado al rol '{Rol}'.", auth.AdminEmail, auth.AdminRole);
            }

            await SeedDemoDataAsync(db, userManager, auth, log);

            return app;
        }

        private static async Task SeedDemoDataAsync(
            ApplicationAuthDbContext db,
            UserManager<ApplicationUser> userManager,
            AuthParams auth,
            ILogger log)
        {
            var demoUser = await userManager.FindByEmailAsync(auth.DemoUserEmail!);
            if (demoUser is null)
            {
                demoUser = new ApplicationUser
                {
                    UserName = auth.DemoUserEmail,
                    Email = auth.DemoUserEmail,
                    EmailConfirmed = true,
                    FirstName = "Ana",
                    LastName = "Quispe",
                    DocumentNumber = "6543210",
                };

                var created = await userManager.CreateAsync(demoUser, auth.DemoUserPassword!);
                if (!created.Succeeded)
                    throw new InvalidOperationException(
                        $"SeedAuth: no se pudo crear el usuario demo '{auth.DemoUserEmail}'. {Errores(created)}");
            }

            if (!await userManager.IsInRoleAsync(demoUser, "Cliente"))
                await userManager.AddToRoleAsync(demoUser, "Cliente");

            var destino = await db.Destinations.SingleOrDefaultAsync(item => item.Name == "Uyuni");
            if (destino is null)
            {
                destino = new Destination
                {
                    Name = "Uyuni",
                    Department = "Potosi",
                    Description = "Descubre el Salar de Uyuni, sus paisajes de sal y la Reserva Eduardo Avaroa.",
                    MainImage = "https://images.unsplash.com/photo-1526392060635-9d6019884377?auto=format&fit=crop&w=1200&q=80",
                    GalleryImages = "https://images.unsplash.com/photo-1526392060635-9d6019884377?auto=format&fit=crop&w=1200&q=80",
                    TouristInfo = "Lleva proteccion solar, ropa abrigada y agua. La mejor temporada es de mayo a octubre.",
                };
                db.Destinations.Add(destino);
            }

            var laPaz = await db.Destinations.SingleOrDefaultAsync(item => item.Name == "La Paz");
            if (laPaz is null)
            {
                laPaz = new Destination
                {
                    Name = "La Paz",
                    Department = "La Paz",
                    Description = "Una ciudad entre montanas, historia viva y el impresionante Valle de la Luna.",
                    MainImage = "https://images.unsplash.com/photo-1589909202802-8f4aadce1849?auto=format&fit=crop&w=1200&q=80",
                    GalleryImages = "https://images.unsplash.com/photo-1589909202802-8f4aadce1849?auto=format&fit=crop&w=1200&q=80",
                    TouristInfo = "Aclimatate antes de realizar actividades exigentes y lleva una chaqueta para las noches.",
                };
                db.Destinations.Add(laPaz);
            }

            var aventura = await db.Categories.SingleOrDefaultAsync(item => item.Name == "Aventura");
            if (aventura is null)
            {
                aventura = new Category { Name = "Aventura", Description = "Experiencias activas y paisajes inolvidables." };
                db.Categories.Add(aventura);
            }

            var cultura = await db.Categories.SingleOrDefaultAsync(item => item.Name == "Cultura");
            if (cultura is null)
            {
                cultura = new Category { Name = "Cultura", Description = "Historia, tradiciones y patrimonio boliviano." };
                db.Categories.Add(cultura);
            }

            await db.SaveChangesAsync();

            var salar = await db.TravelPackages.SingleOrDefaultAsync(item => item.Name == "Salar de Uyuni Esencial");
            if (salar is null)
            {
                salar = new TravelPackage
                {
                    Name = "Salar de Uyuni Esencial",
                    Description = "Tres dias para conocer el salar, la Isla Incahuasi y las lagunas altiplanicas.",
                    DestinationId = destino.Id,
                    Price = 1850m,
                    DurationDays = 3,
                    DurationNights = 2,
                    MainImage = destino.MainImage,
                    IncludedServices = "Transporte 4x4, alojamiento, alimentacion y guia local.",
                    NotIncludedServices = "Vuelos, bebidas y gastos personales.",
                };
                salar.TravelPackageCategories.Add(new TravelPackageCategory { CategoryId = aventura.Id });
                salar.TravelPackageCategories.Add(new TravelPackageCategory { CategoryId = cultura.Id });
                salar.Images.Add(new TravelImage { ImageUrl = destino.MainImage });
                salar.ItineraryDays.Add(new ItineraryDay { DayNumber = 1, Title = "El gran salar", Description = "Salida desde Uyuni, Colchani e Isla Incahuasi." });
                salar.ItineraryDays.Add(new ItineraryDay { DayNumber = 2, Title = "Lagunas altiplanicas", Description = "Ruta de lagunas y paisajes volcanicos." });
                salar.ItineraryDays.Add(new ItineraryDay { DayNumber = 3, Title = "Regreso", Description = "Desayuno y retorno a Uyuni." });
                db.TravelPackages.Add(salar);
            }

            var teleferico = await db.TravelPackages.SingleOrDefaultAsync(item => item.Name == "La Paz y Valle de la Luna");
            if (teleferico is null)
            {
                teleferico = new TravelPackage
                {
                    Name = "La Paz y Valle de la Luna",
                    Description = "Recorrido urbano por La Paz, telefericos, mercados tradicionales y Valle de la Luna.",
                    DestinationId = laPaz.Id,
                    Price = 780m,
                    DurationDays = 2,
                    DurationNights = 1,
                    MainImage = laPaz.MainImage,
                    IncludedServices = "Traslados, guia y entradas indicadas en el itinerario.",
                    NotIncludedServices = "Almuerzos y gastos personales.",
                };
                teleferico.TravelPackageCategories.Add(new TravelPackageCategory { CategoryId = cultura.Id });
                teleferico.Images.Add(new TravelImage { ImageUrl = laPaz.MainImage });
                teleferico.ItineraryDays.Add(new ItineraryDay { DayNumber = 1, Title = "La Paz historica", Description = "Centro historico, mercados y recorrido en teleferico." });
                teleferico.ItineraryDays.Add(new ItineraryDay { DayNumber = 2, Title = "Valle de la Luna", Description = "Visita al Valle de la Luna y retorno." });
                db.TravelPackages.Add(teleferico);
            }

            await db.SaveChangesAsync();

            var departure = await db.TravelDepartures.SingleOrDefaultAsync(item => item.TravelPackageId == salar.Id);
            if (departure is null)
            {
                departure = new TravelDeparture
                {
                    TravelPackageId = salar.Id,
                    DepartureDate = DateTime.UtcNow.Date.AddDays(45),
                    ReturnDate = DateTime.UtcNow.Date.AddDays(48),
                    Capacity = 16,
                    Price = salar.Price,
                    ReservedSeats = 2,
                };
                db.TravelDepartures.Add(departure);
            }

            var secondDeparture = await db.TravelDepartures.SingleOrDefaultAsync(item => item.TravelPackageId == teleferico.Id);
            if (secondDeparture is null)
            {
                db.TravelDepartures.Add(new TravelDeparture
                {
                    TravelPackageId = teleferico.Id,
                    DepartureDate = DateTime.UtcNow.Date.AddDays(20),
                    ReturnDate = DateTime.UtcNow.Date.AddDays(22),
                    Capacity = 20,
                    Price = teleferico.Price,
                });
            }

            await db.SaveChangesAsync();

            if (!await db.Reservations.AnyAsync(item => item.UserId == demoUser.Id))
            {
                var reservation = new Reservation
                {
                    Code = "RES-DEMO-0001",
                    UserId = demoUser.Id,
                    TravelDepartureId = departure.Id,
                    NumberOfPeople = 2,
                    UnitPrice = departure.Price,
                    TotalPrice = departure.Price * 2,
                    Status = ReservationStatus.Confirmada,
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    Observations = "Reserva creada como dato de demostracion.",
                };
                reservation.StatusHistory.Add(new ReservationStatusHistory
                {
                    PreviousStatus = string.Empty,
                    NewStatus = ReservationStatus.Pendiente.ToString(),
                    ChangedAt = reservation.CreatedAt,
                    ChangedByUserId = demoUser.Id,
                });
                reservation.StatusHistory.Add(new ReservationStatusHistory
                {
                    PreviousStatus = ReservationStatus.Pendiente.ToString(),
                    NewStatus = ReservationStatus.Confirmada.ToString(),
                    ChangedAt = reservation.CreatedAt.AddHours(3),
                    ChangedByUserId = demoUser.Id,
                });
                db.Reservations.Add(reservation);
                departure.ReservedSeats += reservation.NumberOfPeople;
                await db.SaveChangesAsync();
            }

            log.LogInformation("SeedAuth: datos demo listos para '{Email}'.", auth.DemoUserEmail);
        }

        private static Task RedirigirOResponder(RedirectContext<CookieAuthenticationOptions> contexto, int codigo)
        {
            if (contexto.Request.Path.StartsWithSegments("/api"))
                contexto.Response.StatusCode = codigo;
            else
                contexto.Response.Redirect(contexto.RedirectUri);

            return Task.CompletedTask;
        }

        private static string Errores(IdentityResult resultado) =>
            string.Join(" ", resultado.Errors.Select(e => $"[{e.Code}] {e.Description}"));
    }

    public class AuthParams
    {
        public const string SectionName = "Auth";

        /// <summary>Rol del administrador. Siempre se crea, esté o no en Roles.</summary>
        public string AdminRole { get; set; } = "Admin";

        /// <summary>Roles que se crean al arrancar. Agregar uno nuevo es editar la configuración.</summary>
        public string[] Roles { get; set; } = [];

        /// <summary>Vacíos ⇒ se crean los roles pero no se siembra el usuario.</summary>
        public string? AdminEmail { get; set; }
        public string? AdminPassword { get; set; }
        public string DemoUserEmail { get; set; } = "usuario@ejemplo.com";
        public string? DemoUserPassword { get; set; }

        public IEnumerable<string> TodosLosRoles() =>
            new[] { AdminRole }
                .Concat(Roles)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase);

        public static AuthParams FromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection(SectionName);

            return section.Exists()
                ? section.Get<AuthParams>() ?? new AuthParams()
                : new AuthParams();
        }
    }
}
