namespace JeDax.Security;

/// <summary>
/// Contexto de usuario del request actual, poblado por el middleware de auth (cookie).
/// Reemplaza UserSession (que dependía del circuito Blazor via IJSRuntime).
/// </summary>
public class CurrentUser
{
    public SessionUser? User { get; private set; }
    public bool IsAuthenticated => User?.IsAuthenticated == true;

    public void Set(SessionUser user) => User = user;
}
