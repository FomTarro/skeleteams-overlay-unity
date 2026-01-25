using System;
using System.Collections.Generic;

namespace Skeletom.BattleStation.Integrations.Twitch
{
    #region Generic API Response

    public interface IDataResponse { }

    [Serializable]
    public class DataResponse<T> : IDataResponse
    {
        public List<T> data;
    }

    #endregion

    #region Validate Token API 

    [Serializable]
    public class TokenValidationResponse
    {
        public string client_id;
        public string login;
        public string[] scopes = new string[0];
        public string user_id;
        public int expires_in;
    }

    #endregion

    #region User Data API

    [Serializable]
    public class UserData
    {
        public string id;
        public string login;
        public string display_name;
        public string type;
        public string broadcaster_type;
        public string description;
        public string profile_image_url;
        public string offline_image_url;
        public int view_count;
        public string email;
        public string created_at;
    }

    #endregion

    #region Emotes API 

    [Serializable]
    public class EmoteDataResponse : DataResponse<EmoteData>
    {
        public string template;
    }

    [Serializable]
    public class EmoteData
    {
        public string id;
        public string name;
        public EmoteImages images;
        public string[] format = new string[0];
        public string[] scale = new string[0];
        public string[] theme_mode = new string[0];

        public EmoteData()
        {

        }

        public EmoteData(string emoteName, EventSub.ChatMessageFragmentEmote fragment)
        {
            name = emoteName;
            id = fragment.id;
            format = fragment.format;
            scale = new string[] { "1.0" };
        }
    }

    [Serializable]
    public class EmoteImages
    {
        public string url_1x;
        public string url_2x;
        public string url_4x;
    }

    #endregion

    #region Badges API

    [Serializable]
    public class BadgeSetData
    {
        public string set_id;
        public BadgeVersionData[] versions;

    }

    [Serializable]
    public class BadgeVersionData
    {
        public string id;
        public string image_url_1x;
        public string image_url_2x;
        public string image_url_4x;
        public string title;
        public string description;
        public string click_action;
        public string click_url;
    }

    #endregion
}
