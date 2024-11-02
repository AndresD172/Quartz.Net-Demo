using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity.UI.Services;
using Quartz.Job;
using Jobs;

namespace Quartz.Net_Demo
{
    public static class QuartzExtension
    {
        public static WebApplicationBuilder ConfigureQuartzServices(this WebApplicationBuilder builder)
        {
            // Configura el servicio de Quartz. Estas configuraciones pueden ser añadidas desde appsettings.json también.
            builder.Services.Configure<QuartzOptions>(options =>
            {
                options.SchedulerName = "Quartz.AspNetCore Demo Scheduler";
                // En conjunto con options.Scheduling.OverWriteExistingData, options.Scheduling.IgnoreDuplicates permite a Quartz
                // sobreescribir trabajos anteriores sin lanzar excepciones.
                options.Scheduling.IgnoreDuplicates = true;
                options.Scheduling.OverWriteExistingData = true;

            });

            builder.Services.AddQuartz(q =>
            {
                // Configura la lectura del schedule. Si hay nuevos trabajos, se añaden a la base de datos.
                q.UseXmlSchedulingConfiguration(x =>
                {
                    // Agrega la referencia al archivo que tiene las definiciones de los trabajos.
                    x.Files = new[] { "../Jobs/JobsSchedule.xml" };
                    x.ScanInterval = TimeSpan.FromMinutes(1);
                    // Lanza una excepción cuando no se encuentra un archivo de configuración.
                    x.FailOnFileNotFound = true;
                    // Lanza una excepción cuando no se puede añadir un trabajo especificado en el archivo de config.
                    x.FailOnSchedulingError = true;
                });

                // Configura la persistencia de los trabajos. Cuando uno es registrado, se guarda esta informacion en la base de datos para ser recuperado después.
                q.UsePersistentStore(s =>
                {
                    // Se asegura que el esquema de la DB sea el correcto. De lo contrario, retorna una excepcion.
                    s.PerformSchemaValidation = true;
                    // Indica que todos los datos de JobDataMaps serán almacenados como string, y por consecuente, como una secuencia nombre-valor. Esto nos ayuda
                    // con los problemas de serialización de objetos más complejos a BLOB.
                    s.UseProperties = true;
                    // Indica el tiempo que se debe de esperar para realizar un nuevo intento de conexión a la DB después de que la conexión haya fallado.
                    s.RetryInterval = TimeSpan.FromSeconds(15);
                    // Indica las configuraciones utilizadas para conectarse al SQL Server.
                    s.UseSqlServer(sqlServer =>
                    {
                        sqlServer.ConnectionString = "Server = localhost; Database = Jobs; Trusted_Connection = True; TrustServerCertificate = True; MultipleActiveResultSets = True;";
                        // Este valor es por defecto.
                        sqlServer.TablePrefix = "QRTZ_";
                    });
                    // Establece JSON como estrategia de serialización de datos.
                    s.UseNewtonsoftJsonSerializer();
                });

                // Se encarga de la conversión entre formatos de tiempo de *nix y Windows.
                q.UseTimeZoneConverter();
            });

            builder.Services.AddQuartzHostedService(options =>
            {
                // En caso de un aborto de proceso, el scheduler no permitirá que se termine la ejecución de la app hasta que todos los trabajos hayan terminado correctamente.
                options.WaitForJobsToComplete = true;
            });

            return builder;
        }
    }
}
