# Configuración de Quartz.Net en proyectos de ASP.NET

## Dependencias necesarias

- `Microsoft.Data.SqlClient`
- `Quartz.AspNetCore`
- `Quartz.Plugins`
- `Quartz.Plugins.TimeZoneConverter`
- `Quartz.Serialization.Json`
- `SendGrid`
- `SendGrid.Extensions.DependencyInjection`

## Configuración de los servicios
> [!NOTE]
> La configuración de los servicios de Quartz.Net está realizada en el archivo `QuartzExtensions.cs`.
> Esto puede ser realizado directamente el archivo `Program.cs`.

Lo primero por hacer será inyectar la dependencia de SendGrid, lo que nos permitirá consumir el cliente email 
directamente en el trabajo. Al momento de realizar la inyección de la dependencia debemos de agregar la
API key usando las opciones de configuración brindadas por la librería. La API key está almacenada el archivo
`appsettings.json`.

```CSharp
builder.Services.AddSendGrid(options =>
            options.ApiKey = builder.Configuration.GetValue<string>("SendGrid:Key"));
```

Seguidamente, dentro de `Program.cs`, hacemos el llamado a la configuración del Quartz. Para ello invocamos el nombre
del método que define esta configuración dentro del archivo `QuartzExtensions.cs`.

```CSharp
builder.ConfigureQuartzServices();
```

### Configuración de Quartz.Net

En nuestro proyecto principal hemos de agregar el archivo `QuartzExtensios.cs`. En dicho archivo creamos 
una clase **estática** con el mismo nombre del archivo y un método **estático** llamado `ConfigureQuartzServices`.
Este método deberá recibir como parámetro la instancia de `WebApplicationBuilder` que enviará el sistema de DI. 
Además, este método deberá retornar el tipo `WebApplicationBuilder` al cual le agregamos nuestras propias configuraciones.
Estos métodos especiales, llamados **extensiones**, nos permiten agregar métodos a un tipo sin tener que crear una subclase
del tipo en cuestión.

```CSharp
namespace Quartz.Net_Demo
{
    public static class QuartzExtension
    {
        public static WebApplicationBuilder ConfigureQuartzServices(this WebApplicationBuilder builder)
```

Quartz.Net tiene una gran cantidad de configuraciones, lo que nos da una gran flexibilidad para customizar nuestro entorno.
En nuestro caso nos interesan tres configuraciones en específico:
* La configuración mediante archivos de texto XML.
* La persistencia de datos mediante una base de datos SQL.
* La conversión automática entre formatos de fecha (*nix <=> Windows).

Lo primero que debemos configurar son las opciones del scheduler. En el scheduler podemos configurar cosas como el nombre
del scheduler, el nombre del hilo principal, etc. Podemos encontrar las opciones disponibles en la 
[documentación del proyecto](https://www.quartz-scheduler.net/documentation/quartz-3.x/configuration/reference.html).

```CSharp
 builder.Services.Configure<QuartzOptions>(options =>
 {
     options.SchedulerName = "Quartz.AspNetCore Demo Scheduler";
     // En conjunto con options.Scheduling.OverWriteExistingData, options.Scheduling.IgnoreDuplicates permite a Quartz
     // sobreescribir trabajos anteriores sin lanzar excepciones.
     options.Scheduling.IgnoreDuplicates = true;
     options.Scheduling.OverWriteExistingData = true;
 });
```

Una vez configurado del scheduler, podemos proceder a la configuración del módulo de persistencia de datos. Para este módulo
haremos uso de SQL Server. Dentro del servidor deberemos de generar el esquema de la base de datos necesario para Quartz, haciendo
uso del script `tables_sqlServer.sql` para dicho fin. 

```CSharp
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
        // Configuramos el string de conexión necesario para que Quartz haga la conexión con la base de datos.
        sqlServer.ConnectionString = "Server = localhost; Database = Jobs; Trusted_Connection = True; TrustServerCertificate = True; MultipleActiveResultSets = True;";
        // Este valor es por defecto.
        sqlServer.TablePrefix = "QRTZ_";
    });
    // Establece JSON como estrategia de serialización de datos.
    s.UseNewtonsoftJsonSerializer();
});
```

El módulo de scheduling con XML no se encuentra integrado dentro del núcleo de la librería, por lo que antes de proceder con la 
configuración, debemos de asegurarnos que la dependencia `Quartz.Plugins` se encuentre instalada. Una vez que la librería se encuentra
instalada, podremos proceder con la configuración del módulo. La configuración de este módulo cuenta con cuatro opciones principales:
1) La lista de archivo de donde se obtendrán las configuraciones (`x.Files`).
2) El intervalo de tiempo en que se parseará el archivo en busca de cambios (`x.ScanInterval`).
3) La opción de habilitar o deshabilitar el lanzamiento de excepciones cuando no se encuetre un archivo (`x.FailOnFileNotFound`).
4) La opción de habilitar o deshabilitar el lanzamiento de excepciones cuando falla la adición de trabajos desde un archivo
(`x.FailOnSchedulingError`).

```CSharp
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
```

Por último indicamos el uso del convertidor de formato de fechas.

```CSharp
q.UseTimeZoneConverter();
```

Al final, el método de configuración de Quartz queda de la siguiente manera:
```CSharp
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
```

# Creación de trabajos

La creación de trabajos en Quartz es bastante sencilla. Lo único que debemos de hacer es implementar la interfaz `IJOb` y el 
método `Execute`. Los trabajos aceptan inyección de dependencias, por lo que seremos capaces de acceder a servicios configurados 
en `Program.cs` de la misma manera que lo haríamos con los controladores de ASP.NET.

En nuestro caso, vamos a configurar un trabajo que envíe un correo electrónico cada 15 segundos. Para ello solicitamos la instancia
de `ISendGridClient` que nos permitirá hacer uso del cliente.

```CSharp
public class EnviarEmail : IJob
{
    private readonly ISendGridClient client;

    // Inyecta las dependencias del cliente de SendGrid
    public EnviarEmail(ISendGridClient client)
    {
        this.client = client;
    }
```

Una vez que tenemos el cliente disponible, podremos continuar con la implementación del método `Execute`. Este método es público, 
asíncrono, retorna el tipo `Task` y recibe como parámetro el contexto de ejecución `IJobExecutionContext`. Cualquier código que
pongamos dentro de su definición será ejecutado a la hora definida por el trigger.

```CSharp
public async Task Execute(IJobExecutionContext context)
{
    SendGridMessage msg = new SendGridMessage
    {
        From = new EmailAddress("andres.chacon.mora@covao.ed.cr"),
        Subject = "Mensaje de prueba enviado mediante SendGrid usando jobs de Quartz.Net",
        PlainTextContent = $"Tarea ejecutada el {DateTime.UtcNow}.",
    };
    msg.AddTo("andreschaconmora3@gmail.com");
    var response = await client.SendEmailAsync(msg);
}
```

Al final, la implementación de un trabajo queda de la siguiente manera:

```CSharp
public class EnviarEmail : IJob
{
    private readonly ISendGridClient client;

    // Inyecta las dependencias del cliente de SendGrid
    public EnviarEmail(ISendGridClient client)
    {
        this.client = client;
    }

    // Mientras las dependencias se encuentre presentes, podemos usar cualquier tipo de lógica dentro de un trabajo.
    public async Task Execute(IJobExecutionContext context)
    {
        SendGridMessage msg = new SendGridMessage
        {
            From = new EmailAddress("andres.chacon.mora@covao.ed.cr"),
            Subject = "Mensaje de prueba enviado mediante SendGrid usando jobs de Quartz.Net",
            PlainTextContent = $"Tarea ejecutada el {DateTime.UtcNow}.",
        };
        msg.AddTo("andreschaconmora3@gmail.com");
        var response = await client.SendEmailAsync(msg);
    }
}
```

# Creación de triggers y registro de trabajos

La creación de triggers y registro de trabajos se lleva a cabo en un archivo de configuración XML. Pueden ser uno o muchos archivos,
los cuales se referencian en las rutas añadidas en la propiedad `x.Files` de la configuración del módulo. En resumen, el módulo
se encarga de parsear toda la información contenida en el XML y registrarla en el scheduler, con el fin de que se realice la ejecución.

Para que un trabajo se reailce, antes hemos de configurar un trigger que dispare dicho evento. El trigger se encarga de mantener la hora
o intervalo en el cual se ejecutará la acción, además de la referencia a la clase que implementa `IJob`. Los triggers no llaman directamenet
a un trabajo, sino que acceden a los DLL y los ejecutan desde ahí, como un "módulo". 

En el XML hemos de establecer primero la referencia del trabajo que queremos ejecutar. Cada trabajo tiene una serie de elementos 
característicos, como un nombre, un grupo (conjunto de trabajos relacionados), una descripción, la referencia a los DLLs del trabajo
y las opciones de disposición del trabajo después de ejecutado.

La estructura básica de un schedule es la siguiente:
```XML
﻿<?xml version="1.0" encoding="utf-8" ?>
<job-scheduling-data xmlns="http://quartznet.sourceforge.net/JobSchedulingData" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" version="2.0">
  <pre-processing-commands>
    <!-- Comandos a ejecutar antes de registrar trabajos y triggers en este archivo. -->
  </pre-processing-commands>
  <processing-directives>
    <!-- Directrices a seguir mientras se registran trabajos y triggers en este archivo. -->
  </processing-directives>
  <schedule>
    <!-- Zona de definición de triggers y trabajos -->
    <job>
      <!-- Zona de definición de características de un trabajo en específico -->
    </job>
    <trigger>
      <!-- Zona de definición de características de un trigger en específico --> 
    </trigger>
  </schedule>
</job-scheduling-data>
```

En nuestro caso utilizaremos la siguiente definición para poner en marcha nuestro trabajo.
```XML
<?xml version="1.0" encoding="utf-8" ?>
<job-scheduling-data xmlns="http://quartznet.sourceforge.net/JobSchedulingData" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" version="2.0">
	<processing-directives>
		<overwrite-existing-data>true</overwrite-existing-data>
	</processing-directives>
	<schedule>
		<job>
			<name>EnviarCorreoAutomatico</name>
			<group>AlertasAutomaticas</group>
			<description>Envía un correo automático a un cliente en una hora dada</description>
			<!-- En job-type debemos de brindar dos argumentos. El primero de ellos indica el nombre de la clase que implementa la interfaz IJob, 
			mientras que el segundo indica el namespace al cual pertenece dicha clase. -->
			<job-type>Jobs.EnviarEmail, Jobs</job-type>
			<!-- Si la propiedad durable es false, para el scheduler el trabajo existirá únicamente mientras exista un trigger asociado a el. 
			Cuando el trigger se complete, el trabajo se eliminaría del scheduler. -->
			<durable>true</durable>
			<!-- La propiedad recover indica si el trabajo de debe de registrar de nuevo cuando ocurra un apagado forzoso (como el crasheo del proceso). -->
			<recover>true</recover>
		</job>
		<trigger>
			<simple>
				<name>EnviarCorreoCadaCinco</name>
				<group>TriggersAlertasAutomaticas</group>
				<description>Envía un correo cada cinco minutos</description>
				<!-- En job-name debemos especificar el NOMBRE EXACTO del trabajo al que se relacionará el trigger. -->
				<job-name>EnviarCorreoAutomatico</job-name>
				<job-group>AlertasAutomaticas</job-group>
				<!-- Deja en manos de la librería el comportamiento de un trigger después de que la ejecución del mismo fallara. -->
				<misfire-instruction>SmartPolicy</misfire-instruction>
				<!-- Indica cuantas veces se puede ejecutar un trigger antes de ser desechado. Si el valor es -1, entonces el trigger nunca será desechado.-->
				<repeat-count>-1</repeat-count>
				<!-- Especifica el intervalo de tiempo entre ejecuciones del trigger. El valor se encuentra en milisegundos. -->
				<repeat-interval>15000</repeat-interval>
			</simple>
		</trigger>
	</schedule>
</job-scheduling-data>
```

Como podemos notar, en la definición de `<job-type>` tenemos dos argumentos. Pues bien, el primer argumento indica la ruta canónica del
clase, mientras que el segundo argumento indica el `namespace ` al que pertenece dicha clase. Es **estrictamente necesario** seguir la
disposición anterior a la hora de definir un `<job-type>`, de lo contrario generará problemas.

# Referencias
- [Referencia de configuración](https://www.quartz-scheduler.net/documentation/quartz-3.x/configuration/reference.html)
- [Documentación de API](https://docs.quartz-scheduler.net/apidoc/3.0/html/d75eb659-6335-53f6-af7a-81814a21ab7f.htm)
- [Código fuente de Quartz.Net](https://github.com/quartznet/quartznet/)
- [Archivo de definición de síntaxis XML para Quartz](https://www.quartz-scheduler.org/xml/job_scheduling_data_2_0.xsd)
- [Inyección de dependencias - Microsoft](https://learn.microsoft.com/es-es/dotnet/core/extensions/dependency-injection)
- [Extensiones - Microsoft](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods)
- [Programación asincrónica basada en tareas - Microsoft](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-based-asynchronous-programming)
