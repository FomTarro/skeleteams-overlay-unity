## Discord IPC Notes

When you join a channel, the `GET_SELECTED_VOICE_CHANNEL` request will respond with a list of `voice_states` including the user info of all users in the channel. If this fires when you're already in the channel, this list will include YOU!

Otherwise, we get a `VOICE_STATE_CREATE` event whenever a user joins the channel, which also contains the user info of that user. User joins also seem to be accompanied by a `SPEAKING_STOP` event.