using Skeletom.BattleStation.Integrations;

public abstract class StreamEventLogGameObject<T> : DisplayableLogGameObject<T, StreamEvent> where T : StreamEventGameObject { }
