using System;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace NAAC.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var server = _config["EmailSettings:SmtpServer"];
            var port = int.Parse(_config["EmailSettings:SmtpPort"] ?? "587");
            var senderEmail = _config["EmailSettings:SenderEmail"];
            var senderName = _config["EmailSettings:SenderName"];
            var appPassword = _config["EmailSettings:AppPassword"];

            using (var message = new MailMessage())
            {
                message.From = new MailAddress(senderEmail!, senderName);
                message.To.Add(new MailAddress(toEmail));
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

                using (var client = new SmtpClient(server, port))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(senderEmail, appPassword);
                    await client.SendMailAsync(message);
                }
            }
        }

        public async Task SendOTPAsync(string toEmail, string otp)
        {
            string subject = "Your NAAC Portal Verification Code";
            string body = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                    <h2 style='color: #0d6efd;'>NAAC System Verification</h2>
                    <p>Hello,</p>
                    <p>Your One-Time Password (OTP) for registration/login is:</p>
                    <div style='font-size: 24px; font-weight: bold; color: #333; padding: 10px; background: #f8f9fa; border-radius: 5px; text-align: center; margin: 20px 0;'>
                        {otp}
                    </div>
                    <p>This code will expire in 10 minutes.</p>
                    <p>If you did not request this code, please ignore this email.</p>
                    <hr style='border: 0; border-top: 1px solid #eee;' />
                    <p style='font-size: 12px; color: #888;'>This is an automated message. Please do not reply.</p>
                </div>";

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendLoginCredentialsAsync(string toEmail, string password, string role)
        {
            string subject = $"{role} Login Credentials - NAAC System";
            string body = $@"
                <div style='font-family: Calibri, sans-serif; font-size: 15px;'>
                    <p>Dear {role},</p>
                    <br/>
                    <p>Login Email: <b>{toEmail}</b></p>
                    <p>Temporary Password: <b>{password}</b></p>
                    <br/>
                    <p>You must change your password after first login.</p>
                </div>";

            await SendEmailAsync(toEmail, subject, body);
        }
    }
}
