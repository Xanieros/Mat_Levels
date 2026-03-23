using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace MatLevels;

#pragma warning disable 8618
internal class Service
{
	[PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; }
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; }
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    internal static void Initialize(IDalamudPluginInterface pluginInterface)
	{
		pluginInterface.Create<Service>();
	}
}
#pragma warning restore 8618
