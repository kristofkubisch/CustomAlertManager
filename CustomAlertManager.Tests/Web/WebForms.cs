using System.Text.RegularExpressions;

namespace CustomAlertManager.Tests.Web;

// Helpers for driving the real pages: fetch the form so the antiforgery token and cookie are set, then post.
public static class WebForms
{
    public const string Password = "Test-password1";

    public static Dictionary<string, string> SignUpFields(string username, string email) => new()
    {
        ["Input.Username"] = username,
        ["Input.Email"] = email,
        ["Input.Password"] = Password,
        ["Input.ConfirmPassword"] = Password,
    };

    public static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string path, Dictionary<string, string> fields)
    {
        var page = await client.GetStringAsync(path);
        var token = Regex.Match(page, @"__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""").Groups[1].Value;
        Assert.NotEmpty(token);

        fields["__RequestVerificationToken"] = token;
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    // Reads the first checkbox with the given name from the page, so a real source can be picked.
    public static async Task<string> FirstCheckboxValueAsync(HttpClient client, string path, string name)
    {
        var page = await client.GetStringAsync(path);
        var value = Regex.Match(page, $@"name=""{name}"" value=""([^""]+)""").Groups[1].Value;
        Assert.NotEmpty(value);
        return value;
    }
}
