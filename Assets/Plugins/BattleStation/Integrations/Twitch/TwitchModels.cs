using System;
using System.Collections.Generic;

namespace Skeletom.BattleStation.Integrations.Twitch
{
    #region Generic API Response

    [Serializable]
    public class DataResponse<T>
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
}
