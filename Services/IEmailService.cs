using System.Threading.Tasks;

namespace NAAC.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendOTPAsync(string toEmail, string otp);
        Task SendLoginCredentialsAsync(string toEmail, string password, string role);
    }
}
