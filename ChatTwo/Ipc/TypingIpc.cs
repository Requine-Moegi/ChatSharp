using ChatTwo.Code;
using Dalamud.Plugin.Ipc;

namespace ChatTwo.Ipc;

using ChatInputState = (bool InputVisible, bool InputFocused, bool HasText, bool IsTyping, int TextLength, ChatType ChannelType);

public sealed class TypingIpc : IDisposable
{
    private Plugin Plugin { get; }

    private readonly List<ICallGateProvider<ChatInputState>> _stateQueryGates = [];
    private readonly List<ICallGateProvider<ChatInputState, object?>> _stateChangedGates = [];

    private ChatInputState LastState;
    private bool HasState;

    public TypingIpc(Plugin plugin)
    {
        Plugin = plugin;

        foreach (var prefix in Plugin.IpcPrefixes)
        {
            var query = Plugin.Interface.GetIpcProvider<ChatInputState>($"{prefix}.GetChatInputState");
            query.RegisterFunc(GetState);
            _stateQueryGates.Add(query);

            _stateChangedGates.Add(Plugin.Interface.GetIpcProvider<ChatInputState, object?>($"{prefix}.ChatInputStateChanged"));
        }
    }

    private ChatInputState BuildState()
    {
        var log = Plugin.ChatLog;

        var usedChannel = Plugin.CurrentTab.CurrentChannel;
        var inputChannel = usedChannel.UseTempChannel ? usedChannel.TempChannel : usedChannel.Channel;
        var channelType = inputChannel.ToChatType();

        return (InputVisible: !log.IsHidden,
            log.InputHandler.InputFocused,
            HasText: log.InputHandler.ChatInput.Length > 0,
            IsTyping: log.InputHandler is { InputFocused: true, ChatInput.Length: > 0 },
            TextLength: log.InputHandler.ChatInput.Length,
            ChannelType: channelType);
    }

    private ChatInputState GetState()
        => BuildState();

    public void Update()
    {
        var state = BuildState();
        if (HasState && state.Equals(LastState))
            return;

        HasState = true;
        LastState = state;
        foreach (var gate in _stateChangedGates)
            gate.SendMessage(state);
    }

    public void Dispose()
    {
        foreach (var gate in _stateQueryGates)
            gate.UnregisterFunc();
    }
}
