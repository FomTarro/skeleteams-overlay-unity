## Discord IPC Notes

When you join a channel, the `GET_SELECTED_VOICE_CHANNEL` request will respond with a list of `voice_states` including the user info of all users in the channel. If this fires when you're already in the channel, this list will include YOU!

Otherwise, we get a `VOICE_STATE_CREATE` event whenever a user joins the channel, which also contains the user info of that user. User joins also seem to be accompanied by a `SPEAKING_STOP` event.

As we add more Managers which emit Events, we hit a glaring issue: Managers want to subscribe to events from other managers, which can create deadlock in initialization order. For example, our Layout Manager wants to listen for new Stream Participants being added. The Participants Manager, meanwhile, loads the participatns list and emits Participant Join events, but it does this on Initialization. Thus, it can emit these before the Layout Manager has attached itself.

We basically want to make sure events are never emitted that early in the lifecycle.

Simiarly, the Discord Integration runs IPC connect immediately on init which could in theory publish channel join events before the overall layout is ready.

Possibly consider the following solutions:
- A central event broker which delays event delivery until the main update loop
- Accompanying Getter methods that can return backfilled events