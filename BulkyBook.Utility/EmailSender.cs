using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkyBook.Utility
{
    public class EmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var emailToSent = new MimeMessage();
            emailToSent.From.Add(MailboxAddress.Parse("hello@dotnet.com"));
            emailToSent.To.Add(MailboxAddress.Parse(email));
            emailToSent.Subject = subject;
            emailToSent.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text= htmlMessage};
            //send email
            using (var emailClient= new SmtpClient())
            {
                emailClient.Connect("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                emailClient.Authenticate("vanshika.jain3152@gmail.com", "ocgz bvrr uzev sfad");
                emailClient.Send(emailToSent);
                emailClient.Disconnect(true);
            }
            return Task.CompletedTask;
        }
    }
}
