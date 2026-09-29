using Skeletom.BattleStation.Integrations;

public abstract class ChatLogGameObject<T> : DisplayableLogGameObject<T, StreamChatMessage> where T : ChatMessageGameObject { }
