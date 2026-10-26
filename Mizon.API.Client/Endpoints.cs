namespace Mizon.API.Client;

public static class Endpoints
{
    private const string BASE_URL = "https://localhost:5128";
    public const string MIZON_HUB_URL = "https://localhost:5128/mizonHub";


    private const string CLIENTS_CONTROLER = $"{BASE_URL}/Clients";
    private const string MUSICS_CONTROLER = $"{BASE_URL}/Musics";
    private const string MEDIAS_CONTROLER = $"{BASE_URL}/Medias";
    private const string STORIES_CONTROLER = $"{BASE_URL}/Stories";
    private const string SEARCH_CONTROLER = $"{BASE_URL}/Search";


    public static class Clients
    {
        public const string LOGIN = $"{CLIENTS_CONTROLER}/Login";
        public const string LOGOUT = $"{CLIENTS_CONTROLER}/Logout";
        public const string ME = $"{CLIENTS_CONTROLER}/Me";
    }


    public static class Musics
    {
        public const string MUSIC = $"{MUSICS_CONTROLER}/Music";
        public const string MusicFileWaveform = $"{MUSICS_CONTROLER}/MusicFileWaveform";
        public const string MusicFile = $"{MUSICS_CONTROLER}/MusicFile";
        public const string MusicStemMix = $"{MUSICS_CONTROLER}/MusicStemMix";
        public const string MusicLyric = $"{MUSICS_CONTROLER}/MusicLyric";
        public const string MusicLyricLine = $"{MUSICS_CONTROLER}/MusicLyricLine";
        public const string MusicReaction = $"{MUSICS_CONTROLER}/MusicReaction";
        public const string MusicReactions = $"{MUSICS_CONTROLER}/MusicReactions";
        public const string MusicReactionTypes = $"{MUSICS_CONTROLER}/MusicReactionTypes";
        public const string MusicGenreTypes = $"{MUSICS_CONTROLER}/MusicGenreTypes";
        public const string MusicAlbumMusics = $"{MUSICS_CONTROLER}/MusicAlbumMusics";
        public const string MusicContributorMusics = $"{MUSICS_CONTROLER}/MusicContributorMusics";
        public const string MusicComments = $"{MUSICS_CONTROLER}/MusicComments";
        public const string MusicCommentCreate = $"{MUSICS_CONTROLER}/MusicCommentCreate";
        public const string MusicContributors = $"{MUSICS_CONTROLER}/MusicContributors";
        public const string MusicView = $"{MUSICS_CONTROLER}/MusicView";
        public const string MusicPlaylists = $"{MUSICS_CONTROLER}/MusicPlaylists";
        public const string MusicPlaylistMembership = $"{MUSICS_CONTROLER}/MusicPlaylistMembership";
        public const string MusicPlaylistCreate = $"{MUSICS_CONTROLER}/MusicPlaylistCreate";

        
    }


    public static class Medias
    {
        public const string Media = $"{MEDIAS_CONTROLER}/Media";

    }

    public static class Stories
    {
        public const string List = $"{STORIES_CONTROLER}/Stories";
        public const string MusicStories = $"{STORIES_CONTROLER}/MusicStories";
        public const string StoryDetails = $"{STORIES_CONTROLER}/StoryDetails";
        public const string StoryView = $"{STORIES_CONTROLER}/StoryView";
        public const string StoryLike = $"{STORIES_CONTROLER}/StoryLike";
        public const string StoryCreate = $"{STORIES_CONTROLER}/StoryCreate";
    }

    public static class Search
    {
        public const string Global = $"{SEARCH_CONTROLER}/Global";
    }
}
