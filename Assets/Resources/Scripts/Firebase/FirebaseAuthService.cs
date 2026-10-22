using Firebase.Auth;
using System.Threading.Tasks;

// Exception riêng cho trường hợp email chưa xác minh
public class EmailNotVerifiedException : System.Exception
{
    public EmailNotVerifiedException() : base("Email not verified.") { }
}

public class FirebaseAuthService
{
    private readonly PlayerDatabase _playerDatabase = new();

    public async Task Register(
        string email,
        string password,
        string fullName,
        string nickname)
    {
        AuthResult result =
            await FirebaseManager.Instance.Auth
            .CreateUserWithEmailAndPasswordAsync(email, password);

        FirebaseUser user = result.User;

        PlayerData player = new(
            user.UserId,
            user.Email,
            fullName,
            nickname);

        await _playerDatabase.SavePlayer(player);

        await user.SendEmailVerificationAsync();
    }

    public async Task VerifyPlayer()
    {
        FirebaseUser user = FirebaseManager.Instance.Auth.CurrentUser;

        if (user == null) return;

        await _playerDatabase.VerifyPlayer(user.UserId);
    }

    public async Task Login(string email, string password)
    {
        AuthResult result =
            await FirebaseManager.Instance.Auth
            .SignInWithEmailAndPasswordAsync(email, password);

        FirebaseUser user = result.User;

        await user.ReloadAsync();
        user = FirebaseManager.Instance.Auth.CurrentUser;

        if (user != null && !user.IsEmailVerified)
        {
            FirebaseManager.Instance.Auth.SignOut();
            throw new EmailNotVerifiedException();
        }
    }

    public async Task<PlayerData> GetCurrentPlayer()
    {
        FirebaseUser user = FirebaseManager.Instance.Auth.CurrentUser;

        if (user == null) return null;

        return await _playerDatabase.LoadPlayer(user.UserId);
    }

    public async Task DeleteCurrentUser()
    {
        FirebaseUser user = FirebaseManager.Instance.Auth.CurrentUser;

        if (user == null) return;

        await _playerDatabase.DeletePlayer(user.UserId);

        await user.DeleteAsync();

        FirebaseManager.Instance.Auth.SignOut();
    }

    public void Logout()
    {
        FirebaseManager.Instance.Auth.SignOut();
    }
}