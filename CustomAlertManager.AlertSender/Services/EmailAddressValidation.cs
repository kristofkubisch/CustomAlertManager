using System.Net.Mail;

namespace CustomAlertManager.AlertSender.Services;

public static class EmailAddressValidation
{
    public static bool IsValid(string address)
    {
        try
        {
            _ = new MailAddress(address);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
