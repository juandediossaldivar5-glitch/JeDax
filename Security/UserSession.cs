using JeDax.Data;
using Microsoft.JSInterop;
using System.Text.Json;

namespace JeDax.Security;

public class UserSession
{
    private const string StorageKey = "jedax_session";
    private readonly IJSRuntime _js;
    private readonly TenantContext _tenant;
    private SessionUser? _cached;

    public event Action? OnChange;

    public UserSession(IJSRuntime js, TenantContext tenant)
    {
        _js = js;
        _tenant = tenant;
    }

    public bool IsAuthenticated => _cached?.IsAuthenticated == true;
    public SessionUser? User => _cached;

    public async Task SetAsync(SessionUser user)
    {
        _cached = user;
        _tenant.Set(user.TenantId, user.TenantSlug);
        try
        {
            var json = JsonSerializer.Serialize(user);
            await _js.InvokeVoidAsync("sessionSave", StorageKey, json);
        }
        catch { /* JS interop unavailable during SSR */ }
        OnChange?.Invoke();
    }

    public async Task LoadAsync()
    {
        if (_cached is not null) return;
        try
        {
            var json = await _js.InvokeAsync<string?>("sessionLoad", StorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                _cached = JsonSerializer.Deserialize<SessionUser>(json);
                if (_cached is not null)
                    _tenant.Set(_cached.TenantId, _cached.TenantSlug);
            }
        }
        catch { /* JS interop unavailable during SSR */ }
    }

    public async Task ClearAsync()
    {
        _cached = null;
        try { await _js.InvokeVoidAsync("sessionClear", StorageKey); }
        catch { }
        OnChange?.Invoke();
    }
}
