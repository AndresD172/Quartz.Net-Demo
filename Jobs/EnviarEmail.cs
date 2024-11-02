using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Job;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Jobs
{
    // La interfaz IJob representa el "trabajo" a ejecutar por el scheduler. Se debe implementar un método asíncrono llamado "execute", el cual es 
    // utilizado para ejecutar el trabajo por el scheduler. Si se trabaja con un framework de inyección de dependencias, podemos consumir una 
    // dependencia tal y como lo haríamos en un controlador de ASP.NET
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
}
