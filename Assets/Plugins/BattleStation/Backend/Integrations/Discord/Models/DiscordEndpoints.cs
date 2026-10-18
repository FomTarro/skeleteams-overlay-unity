namespace Skeletom.BattleStation.Integrations.Discord
{
    public interface IDiscordEndpoints
    {
        public string USER_TOKEN_ENDPOINT { get; }
        public string USER_INFO_ENDPOINT { get; }
    }

    public struct DiscordAPI : IDiscordEndpoints
    {
        public readonly string USER_TOKEN_ENDPOINT => "https://discord.com/api/oauth2/token";
        public readonly string USER_INFO_ENDPOINT => "https://discord.com/api/v10/users/";
    }
}
