using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Ipc;

namespace ChatTwo;

public sealed class IpcManager : IDisposable
{
    private readonly List<ICallGateProvider<string>> _registerGates = [];
    private readonly List<ICallGateProvider<string, object?>> _unregisterGates = [];
    private readonly List<ICallGateProvider<object?>> _availableGates = [];
    private readonly List<ICallGateProvider<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?>> _invokeGates = [];

    public List<string> Registered { get; } = [];

    public IpcManager()
    {
        foreach (var prefix in Plugin.IpcPrefixes)
        {
            var register = Plugin.Interface.GetIpcProvider<string>($"{prefix}.Register");
            register.RegisterFunc(Register);
            _registerGates.Add(register);

            var available = Plugin.Interface.GetIpcProvider<object?>($"{prefix}.Available");
            _availableGates.Add(available);

            var unregister = Plugin.Interface.GetIpcProvider<string, object?>($"{prefix}.Unregister");
            unregister.RegisterAction(Unregister);
            _unregisterGates.Add(unregister);

            var invoke = Plugin.Interface.GetIpcProvider<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?>($"{prefix}.Invoke");
            _invokeGates.Add(invoke);
        }

        foreach (var gate in _availableGates)
            gate.SendMessage();
    }

    public void Invoke(string id, PlayerPayload? sender, ulong contentId, Payload? payload, SeString? senderString, SeString? content)
    {
        foreach (var gate in _invokeGates)
            gate.SendMessage(id, sender, contentId, payload, senderString, content);
    }

    private string Register()
    {
        var id = Guid.NewGuid().ToString();
        Registered.Add(id);
        return id;
    }

    private void Unregister(string id)
    {
        Registered.Remove(id);
    }

    public void Dispose()
    {
        foreach (var gate in _unregisterGates)
            gate.UnregisterAction();
        foreach (var gate in _registerGates)
            gate.UnregisterFunc();
        Registered.Clear();
    }
}
