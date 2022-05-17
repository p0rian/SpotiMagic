using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SpotiMagic
{
    internal class Program
    {
        
        private static EmbedIOAuthServer server;

        private static string clientId = "clientID";
        private static string clientSecret = "clientSecret";
        private static string ID;
        private static SpotifyClient spotify;
        
        static void DoMenuLoop()
        {
            while(true)
            {
                Console.WriteLine("[[[ SpotiMagic ]]]");
                Console.WriteLine("1. Backup your library");
                Console.WriteLine("2. Filter songs by artist");
                Console.WriteLine("3. Import songs to Spotify");
                Console.WriteLine("4. Print backup");
                Console.WriteLine("5. Get song's artists");
                Console.WriteLine("6. Exit");

                int choice = Int32.Parse(Console.ReadLine());

                switch(choice)
                {
                    case 1:
                        BackupUserPlaylists(spotify).Wait();
                        break;
                    case 2:
                        Console.WriteLine("Input an artist name: ");
                        string artistName = Console.ReadLine();
                        var result = FindTrackByArtist(artistName);
                        foreach(var track in result)
                        {
                            Console.WriteLine(track);
                        }
                        break;
                    case 3:
                        Console.WriteLine("Input an existing playlist name: ");
                        string existingPlaylist = Console.ReadLine();
                        Console.WriteLine("Input name for the new playlist: ");
                        string newPlaylist = Console.ReadLine();
                        ImportPlaylist(existingPlaylist, spotify, newPlaylist).Wait();
                        break;
                    case 4:
                        PrintBackup(ReadBackup());
                        break;
                    case 5:
                        Console.WriteLine("Input a song name: ");
                        string trackName = Console.ReadLine();
                        var artists = findArtistsOfTrack(trackName);
                        foreach (var artist in artists)
                        {
                            Console.WriteLine(artist);
                        }
                        break;
                    case 6:
                        return;
                    default:
                        break;
                }
            }
        }
        
        /// <summary>
        /// Returns the PlaylistTrack name
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        static Track GetPlaylistTrackName(PlaylistTrack<IPlayableItem> item)
        {
            if(item.Track is FullTrack track)
            {
                //   return track.Name;
                List<string> artists = new List<string>();
                foreach(var artist in track.Artists){
                    artists.Add(artist.Name);
                }
                return new Track() {Name = track.Name, Artists = artists, trackID = track.Id };
            }
            if(item.Track is FullEpisode episode)
            {
                return new Track() { Name = episode.Name };
            }
            return null;
        }

        /// <summary>
        /// Gets the track names list from the specified playlist
        /// </summary>
        /// <param name="playlistId"></param>
        /// <param name="spotifyClient"></param>
        /// <returns>List of track names</returns>
        static async Task<List<Track>> ScrapePlaylist(string playlistId, SpotifyClient spotifyClient)
        {
            var page = await spotifyClient.Playlists.GetItems(playlistId);
            var tracks = await spotifyClient.PaginateAll(page);
            List<Track> tracksToAdd = new List<Track>();
            foreach (var item in tracks)
            {
                tracksToAdd.Add(GetPlaylistTrackName(item));
            }
            return tracksToAdd;
        }

        /// <summary>
        /// Creates folders with playlist names with empty files of song names inside
        /// </summary>
        /// <param name="spotifyClient"></param>
        static async Task BackupUserPlaylists(SpotifyClient spotifyClient)
        {
            var playlists = GetAllPlaylists(spotifyClient).Result;
            string mainPath = @"c:\Backup";
            foreach (var playlist in playlists)
            {
                var playlistPath = Path.Combine(mainPath, ReplaceIllegalCharacters(playlist.Name));
                Directory.CreateDirectory(playlistPath);
                List<Track> tracks = await ScrapePlaylist(playlist.Id, spotifyClient);
                foreach (var track in tracks)
                {
                    var file = File.Create(Path.Combine(playlistPath, ReplaceIllegalCharacters(track.Name)+".txt"));
                    using (StreamWriter outputFile = new StreamWriter(file))
                    {
                        outputFile.WriteLine(track.Artists.Count);
                        foreach(var artist in track.Artists)
                        {
                            outputFile.WriteLine(artist);
                        }
                        outputFile.WriteLine(track.trackID);
                    }
                    file.Close();
                }
            }
            Console.WriteLine("Library back-up complete");
        }

        /// <summary>
        /// Returns all playlists of the current user
        /// </summary>
        /// <param name="spotifyClient"></param>
        /// <returns></returns>
        static async Task<List<SimplePlaylist>> GetAllPlaylists(SpotifyClient spotifyClient)
        {
            var page = await spotifyClient.Playlists.CurrentUsers();
            return (List<SimplePlaylist>)await spotifyClient.PaginateAll(page);
        }

        /// <summary>
        /// Returns a list with information about which playlists and songs exists only locally and which exist only on Spotify
        /// </summary>
        /// <returns></returns>
        static async Task<List<string>> CompareBackupAndCurrentContent(SpotifyClient spotifyClient)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns a list of backed-up playlists and songs
        /// </summary>
        /// <returns></returns>
        static List<Playlist> ReadBackup()
        {
            List<Playlist> result = new List<Playlist>();
            string mainPath = @"c:\Backup";
            var playlistNames = Directory.GetDirectories(mainPath);
            foreach (var playlistPath in playlistNames)
            {
                var playlistToAdd = new Playlist() { Name = Path.GetFileName(playlistPath), Tracks=new List<Track>()};
                var trackNames = Directory.GetFiles(playlistPath);
                foreach (var trackName in trackNames)
                {
                    var trackToAdd = new Track() { Name = Path.GetFileName(trackName) };
                    playlistToAdd.Tracks.Add(trackToAdd);
                }
                result.Add(playlistToAdd);
            }
            return result;

        }

        /// <summary>
        /// Prints backed-up playlists and their tracks to the console
        /// </summary>
        /// <param name="backup"></param>
        static void PrintBackup(List<Playlist> backup)
        {
            foreach(var playlist in backup)
            {
                Console.WriteLine("Playlist: " + playlist.Name);
                foreach (var track in playlist.Tracks)
                {
                    Console.WriteLine("    " + track.Name);
                }
            }
        }
        static List<string> FindTrackByArtist(string artist)
        {
            string mainPath = @"c:\Backup";
            var playlistNames = Directory.GetDirectories(mainPath);
            List<string> results = new List<string>();
            foreach(var playlistPath in playlistNames)
            {
                var trackPaths = Directory.GetFiles(playlistPath);
                foreach(var trackPath in trackPaths)
                {
                    List<string> songData = new List<string>();
                    var file = File.Open(trackPath, FileMode.Open);
                    using (StreamReader sr = new StreamReader(file))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            songData.Add(line);
                        }
                    }
                    for (var i = 1; i < 1 + int.Parse(songData[0]); i++)
                    {
                        if (songData[i] == artist)
                        {
                            results.Add(Path.GetFileNameWithoutExtension(trackPath));
                            break;
                        }
                    }
                    file.Close();
                    songData.Clear();
                }
            }

            return results;
        }
        static List<string> ScrapeIDs(string playlistName)
        {
            List<string> trackIDs = new List<string>();
            string mainpath = @"c:\Backup";
            var playlistPath = Path.Combine(mainpath, playlistName);
            var trackPaths = Directory.GetFiles(playlistPath);
            foreach (var trackPath in trackPaths)
            {
                List<string> songData = new List<string>();
                var file = File.Open(trackPath, FileMode.Open);
                using (StreamReader sr = new StreamReader(file))
                {   
                    string line;
                    while((line = sr.ReadLine()) != null)
                    {
                        songData.Add(line);
                    }
                }
                trackIDs.Add(songData[1 + int.Parse(songData[0])]);
                file.Close();
                songData.Clear();
            }
            return trackIDs;
           
        }
        static async Task<FullPlaylist> CreatePlaylist(string playlistName, SpotifyClient spotifyClient)
        {
            var request = new PlaylistCreateRequest(playlistName);
            var response = await spotifyClient.Playlists.Create(ID, request);
            return response;
        }
        static async Task ImportPlaylist(string playlistName, SpotifyClient spotifyClient, string newPlaylistName)
        {
            
            var newPlaylist = await CreatePlaylist(newPlaylistName, spotifyClient);
            List<string> URIs = new List<string>();
            foreach (var trackID in ScrapeIDs(playlistName))
            {
                URIs.Add("spotify:track:"+trackID);
            }
            var request = new PlaylistAddItemsRequest ( URIs);
            var response = await spotifyClient.Playlists.AddItems(newPlaylist.Id, request);
            Console.WriteLine("Playlist created as {0}", newPlaylist.Name);
            
        }
        static List<string> findArtistsOfTrack(string trackName)
        {
            string mainPath = @"c:\Backup";
            var playlistNames = Directory.GetDirectories(mainPath);
            List<string> results = new List<string>();
            foreach (var playlistPath in playlistNames)
            {
                var trackPaths = Directory.GetFiles(playlistPath);
                foreach (var trackPath in trackPaths)
                {
                    List<string> songData = new List<string>();
                    if (trackName == Path.GetFileNameWithoutExtension(trackPath))
                    {
                        var file = File.Open(trackPath, FileMode.Open);
                        using (StreamReader sr = new StreamReader(file))
                        {
                            string line;
                            while ((line = sr.ReadLine()) != null)
                            {
                                songData.Add(line);
                            }
                        }
                        for (var i = 1; i < 1 + int.Parse(songData[0]); i++)
                        {
                            results.Add(songData[i]);
                        }
                        file.Close();
                        songData.Clear();

                    }
                }
            }
            return results;
        }
        /// <summary>
        /// Cleans the string from characters that can't be used in pathnames
        /// </summary>
        /// <param name="name"></param>
        /// <returns>Legal name</returns>
        static string ReplaceIllegalCharacters(string name)
        {
            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex r = new Regex(string.Format("[{0}]", Regex.Escape(invalid)));
            return r.Replace(name, "").TrimEnd();
        }

        private static async Task Authorize()
        {
            server = new EmbedIOAuthServer(new Uri("http://localhost:5000/callback"), 5000);
            await server.Start();
            server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
            server.ErrorReceived += OnErrorReceived;

            var request = new LoginRequest(server.BaseUri, clientId, LoginRequest.ResponseType.Code)
            {
                Scope = new List<string> { Scopes.PlaylistReadPrivate, Scopes.PlaylistModifyPrivate, Scopes.PlaylistModifyPublic }
            };

            BrowserUtil.Open(request.ToUri());
        }

        private static async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            await server.Stop();
            var config = SpotifyClientConfig.CreateDefault();
            var tokenResponse = await new OAuthClient(config).RequestToken(
                new AuthorizationCodeTokenRequest(
                    clientId, clientSecret, response.Code, new Uri("http://localhost:5000/callback")
                    )
                );

            spotify = new SpotifyClient(tokenResponse.AccessToken);
            ID = spotify.UserProfile.Current().Result.Id;
        }

        private static async Task OnErrorReceived(object sender, string error, string state)
        {
            Console.WriteLine($"Aborting authorization, error received: {error}");
            await server.Stop();
        }

        static void Main(string[] args)
        {
            Authorize().Wait();

            DoMenuLoop();
        }
    }
}
