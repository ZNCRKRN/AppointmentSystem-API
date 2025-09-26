using AppointmentSystem.API.DTOs;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace AppointmentSystem.API.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendAppointmentConfirmationAsync(AppointmentResponse appointment, string recipientEmail, string recipientName)
        {
            var subject = "Randevu Onaylandı - Appointment System";
            var body = $@"
                <html>
                <body>
                    <h2>Randevu Onaylandı</h2>
                    <p>Merhaba {recipientName},</p>
                    <p>Randevunuz başarıyla onaylanmıştır.</p>
                    <br>
                    <h3>Randevu Detayları:</h3>
                    <ul>
                        <li><strong>Konu:</strong> {appointment.Subject}</li>
                        <li><strong>Tarih:</strong> {appointment.StartTime:dd MMMM yyyy, dddd}</li>
                        <li><strong>Saat:</strong> {appointment.StartTime:HH:mm} - {appointment.EndTime:HH:mm}</li>
                        <li><strong>Danışman:</strong> {appointment.AdvisorName}</li>
                        <li><strong>Öğrenci:</strong> {appointment.StudentName}</li>
                        <li><strong>Durum:</strong> {appointment.Status}</li>
                    </ul>
                    {(!string.IsNullOrEmpty(appointment.Description) ? $"<p><strong>Açıklama:</strong> {appointment.Description}</p>" : "")}
                    <br>
                    <p>İyi günler dileriz.</p>
                    <p>Appointment System Ekibi</p>
                </body>
                </html>";

            await SendEmailAsync(recipientEmail, recipientName, subject, body);
        }

        public async Task SendAppointmentReminderAsync(AppointmentResponse appointment, string recipientEmail, string recipientName)
        {
            var subject = "Randevu Hatırlatması - Appointment System";
            var body = $@"
                <html>
                <body>
                    <h2>Randevu Hatırlatması</h2>
                    <p>Merhaba {recipientName},</p>
                    <p>Yarın saat {appointment.StartTime:HH:mm}'de randevunuz bulunmaktadır.</p>
                    <br>
                    <h3>Randevu Detayları:</h3>
                    <ul>
                        <li><strong>Konu:</strong> {appointment.Subject}</li>
                        <li><strong>Tarih:</strong> {appointment.StartTime:dd MMMM yyyy, dddd}</li>
                        <li><strong>Saat:</strong> {appointment.StartTime:HH:mm} - {appointment.EndTime:HH:mm}</li>
                        <li><strong>Danışman:</strong> {appointment.AdvisorName}</li>
                        <li><strong>Öğrenci:</strong> {appointment.StudentName}</li>
                    </ul>
                    <br>
                    <p>Lütfen randevu saatinde hazır bulunun.</p>
                    <p>Appointment System Ekibi</p>
                </body>
                </html>";

            await SendEmailAsync(recipientEmail, recipientName, subject, body);
        }

        public async Task SendAppointmentCancellationAsync(AppointmentResponse appointment, string recipientEmail, string recipientName)
        {
            var subject = "Randevu İptal Edildi - Appointment System";
            var body = $@"
                <html>
                <body>
                    <h2>Randevu İptal Edildi</h2>
                    <p>Merhaba {recipientName},</p>
                    <p>Maalesef randevunuz iptal edilmiştir.</p>
                    <br>
                    <h3>İptal Edilen Randevu Detayları:</h3>
                    <ul>
                        <li><strong>Konu:</strong> {appointment.Subject}</li>
                        <li><strong>Tarih:</strong> {appointment.StartTime:dd MMMM yyyy, dddd}</li>
                        <li><strong>Saat:</strong> {appointment.StartTime:HH:mm} - {appointment.EndTime:HH:mm}</li>
                        <li><strong>Danışman:</strong> {appointment.AdvisorName}</li>
                        <li><strong>Öğrenci:</strong> {appointment.StudentName}</li>
                    </ul>
                    <br>
                    <p>Yeni bir randevu oluşturmak için sistemimizi kullanabilirsiniz.</p>
                    <p>Appointment System Ekibi</p>
                </body>
                </html>";

            await SendEmailAsync(recipientEmail, recipientName, subject, body);
        }

        public async Task SendWelcomeEmailAsync(string email, string name, string role)
        {
            var subject = "Hoş Geldiniz - Appointment System";
            var roleText = role == "Student" ? "Öğrenci" : role == "Advisor" ? "Danışman" : "Admin";
            
            var body = $@"
                <html>
                <body>
                    <h2>Hoş Geldiniz!</h2>
                    <p>Merhaba {name},</p>
                    <p>Appointment System'e hoş geldiniz! Hesabınız başarıyla oluşturulmuştur.</p>
                    <br>
                    <p><strong>Rolünüz:</strong> {roleText}</p>
                    <br>
                    <p>Artık sistemimizi kullanarak:</p>
                    <ul>
                        {(role == "Student" ? "<li>Danışmanlarla randevu oluşturabilirsiniz</li>" : "")}
                        {(role == "Advisor" ? "<li>Müsaitlik takviminizi yönetebilirsiniz</li><li>Öğrenci randevularını onaylayabilirsiniz</li>" : "")}
                        {(role == "Admin" ? "<li>Tüm sistemi yönetebilirsiniz</li>" : "")}
                    </ul>
                    <br>
                    <p>İyi kullanımlar dileriz!</p>
                    <p>Appointment System Ekibi</p>
                </body>
                </html>";

            await SendEmailAsync(email, name, subject, body);
        }

        public async Task SendPasswordResetEmailAsync(string email, string resetToken)
        {
            var subject = "Şifre Sıfırlama - Appointment System";
            var resetUrl = $"{_configuration["FrontendUrl"]}/reset-password?token={resetToken}";
            
            var body = $@"
                <html>
                <body>
                    <h2>Şifre Sıfırlama</h2>
                    <p>Merhaba,</p>
                    <p>Şifre sıfırlama talebinde bulundunuz. Aşağıdaki bağlantıya tıklayarak yeni şifrenizi oluşturabilirsiniz:</p>
                    <br>
                    <p><a href=""{resetUrl}"" style=""background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;"">Şifremi Sıfırla</a></p>
                    <br>
                    <p>Bu bağlantı 24 saat geçerlidir.</p>
                    <p>Eğer bu talebi siz yapmadıysanız, bu e-postayı görmezden gelebilirsiniz.</p>
                    <br>
                    <p>Appointment System Ekibi</p>
                </body>
                </html>";

            await SendEmailAsync(email, "", subject, body);
        }

        private async Task SendEmailAsync(string toEmail, string toName, string subject, string body)
        {
            try
            {
                var emailSettings = _configuration.GetSection("Email");
                var smtpServer = emailSettings["SmtpServer"];
                var smtpPort = int.Parse(emailSettings["SmtpPort"] ?? "587");
                var smtpUsername = emailSettings["SmtpUsername"];
                var smtpPassword = emailSettings["SmtpPassword"];
                var fromEmail = emailSettings["FromEmail"];
                var fromName = emailSettings["FromName"];

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(new MailboxAddress(toName, toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = body
                };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(smtpUsername, smtpPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
                throw;
            }
        }
    }
}

