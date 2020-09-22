﻿using BeatSaverApi.Entities;
using BeatSaverApi.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace BeatSaverApi
{
    public class BeatSaver
    {
        private readonly string beatSaver;
        private readonly string beatSaverApi;
        private readonly string beatSaverHotApi;
        private readonly string beatSaverRatingApi;
        private readonly string beatSaverLatestApi;
        private readonly string beatSaverDownloadsApi;
        private readonly string beatSaverPlaysApi;
        private readonly string beatSaverSearchApi;
        private readonly string beatSaverDetailsKeyApi;
        private readonly string beatSaverDetailsHashApi;
        private readonly string downloadPath;
        private readonly string[] excludedCharacters;

        public string SongsPath;

        public event EventHandler<DownloadStartedEventArgs> DownloadStarted;
        public event EventHandler<DownloadCompletedEventArgs> DownloadCompleted;

        public BeatSaver(string songsPath)
        {
            beatSaver = "https://beatsaver.com";
            beatSaverApi = $"{beatSaver}/api";
            beatSaverHotApi = $"{beatSaverApi}/maps/hot";
            beatSaverRatingApi = $"{beatSaverApi}/maps/rating";
            beatSaverLatestApi = $"{beatSaverApi}/maps/latest";
            beatSaverDownloadsApi = $"{beatSaverApi}/maps/downloads";
            beatSaverPlaysApi = $"{beatSaverApi}/maps/plays";
            beatSaverSearchApi = $"{beatSaverApi}/search/text";
            beatSaverDetailsKeyApi = $"{beatSaverApi}/maps/detail";
            beatSaverDetailsHashApi = $"{beatSaverApi}/maps/by-hash";
            SongsPath = songsPath;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            downloadPath = $@"{appData}\BeatSaverApi";

            if (!Directory.Exists(downloadPath))
                Directory.CreateDirectory(downloadPath);

            excludedCharacters = new string[]
            {
                "<",
                ">",
                ":",
                "/",
                @"\",
                "|",
                "?",
                "*"
            };

            DownloadCompleted += BeatSaver_DownloadCompleted;
            DownloadStarted += BeatSaver_DownloadStarted;
        }

        private void BeatSaver_DownloadStarted(object sender, DownloadStartedEventArgs e)
        {
            e.Song.IsDownloading = true;
        }

        private void BeatSaver_DownloadCompleted(object sender, DownloadCompletedEventArgs e)
        {
            e.Song.IsDownloading = false;
            e.Song.IsDownloaded = true;
        }

        public async Task<OnlineBeatmaps> GetOnlineBeatmaps(MapSort mapSort, int page = 0)
        {
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    webClient.Headers.Add(HttpRequestHeader.UserAgent, "BeatSaverApi");
                    string json = null;

                    switch (mapSort)
                    {
                        case MapSort.Hot:
                            json = await webClient.DownloadStringTaskAsync($"{beatSaverHotApi}/{page}");
                            break;
                        case MapSort.Rating:
                            json = await webClient.DownloadStringTaskAsync($"{beatSaverRatingApi}/{page}");
                            break;
                        case MapSort.Latest:
                            json = await webClient.DownloadStringTaskAsync($"{beatSaverLatestApi}/{page}");
                            break;
                        case MapSort.Downloads:
                            json = await webClient.DownloadStringTaskAsync($"{beatSaverDownloadsApi}/{page}");
                            break;
                        case MapSort.Plays:
                            json = await webClient.DownloadStringTaskAsync($"{beatSaverPlaysApi}/{page}");
                            break;
                        default:
                            break;
                    }

                    OnlineBeatmaps beatSaverMaps = JsonConvert.DeserializeObject<OnlineBeatmaps>(json);
                    string[] songsDownloaded = Directory.GetDirectories(SongsPath);

                    foreach (OnlineBeatmap song in beatSaverMaps.Maps)
                        foreach (string directory in Directory.GetDirectories(SongsPath))
                            song.IsDownloaded = songsDownloaded.Any(x => new DirectoryInfo(x).Name.Split(" ")[0] == song.Key);

                    return beatSaverMaps;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<OnlineBeatmaps> GetOnlineBeatmaps(string query, int page = 0)
        {
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    webClient.Headers.Add(HttpRequestHeader.UserAgent, "BeatSaverApi");
                    string json = await webClient.DownloadStringTaskAsync($"{beatSaverSearchApi}/{page}?q={query}");

                    OnlineBeatmaps beatSaverMaps = JsonConvert.DeserializeObject<OnlineBeatmaps>(json);
                    string[] songsDownloaded = Directory.GetDirectories(SongsPath);

                    foreach (OnlineBeatmap song in beatSaverMaps.Maps)
                    {
                        foreach (string directory in Directory.GetDirectories(SongsPath))
                            song.IsDownloaded = songsDownloaded.Any(x => new DirectoryInfo(x).Name == song.Hash);
                    }

                    return beatSaverMaps;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<LocalBeatmaps> GetLocalBeatmaps(LocalBeatmaps cachedLocalBeatmaps = null)
        {
            LocalBeatmaps localBeatmaps = cachedLocalBeatmaps is null ? new LocalBeatmaps() : new LocalBeatmaps(cachedLocalBeatmaps);
            List<string> songs = Directory.GetDirectories(SongsPath).ToList();

            foreach (LocalBeatmap beatmap in localBeatmaps.Maps.ToList())
            {
                string song = songs.FirstOrDefault(x => new DirectoryInfo(x).Name.Split(" ")[0] == beatmap.Identifier.Value);

                if (song != null)
                    songs.Remove(song);
            }

            for (int i = 0; i < songs.Count; i++)
            {
                if (i > 0 && i % 10 == 0)
                    localBeatmaps.LastPage++;
            }

            foreach (string songFolder in songs)
            {
                string infoFile = $@"{songFolder}\info.dat";
                string[] folderName = new DirectoryInfo(songFolder).Name.Split(" ");
                LocalIdentifier identifier = folderName.Length == 1 ? new LocalIdentifier(false, folderName[0]) : new LocalIdentifier(true, folderName[0]);

                if (!File.Exists(infoFile))
                    continue;

                string json = await File.ReadAllTextAsync(infoFile);
                LocalBeatmap beatmap = JsonConvert.DeserializeObject<LocalBeatmap>(json);

                beatmap.CoverImagePath = $@"{songFolder}\{beatmap.CoverImageFilename}";
                beatmap.Identifier = identifier;
                beatmap.FolderPath = songFolder;

                DifficultyBeatmapSet difficultyBeatmapSet = beatmap.DifficultyBeatmapSets[0];
                if (difficultyBeatmapSet.DifficultyBeatmaps.Any(x => x.Difficulty == "Easy"))
                    beatmap.Easy = true;
                if (difficultyBeatmapSet.DifficultyBeatmaps.Any(x => x.Difficulty == "Normal"))
                    beatmap.Normal = true;
                if (difficultyBeatmapSet.DifficultyBeatmaps.Any(x => x.Difficulty == "Hard"))
                    beatmap.Hard = true;
                if (difficultyBeatmapSet.DifficultyBeatmaps.Any(x => x.Difficulty == "Expert"))
                    beatmap.Expert = true;
                if (difficultyBeatmapSet.DifficultyBeatmaps.Any(x => x.Difficulty == "ExpertPlus"))
                    beatmap.ExpertPlus = true;

                _ = Task.Run(async () => beatmap.OnlineBeatmap = await GetBeatmap(identifier));

                _ = Task.Run(async () =>
                {
                    List<LocalBeatmapDetails> localBeatmapDetails = await GetLocalBeatmapDetails(beatmap, beatmap.DifficultyBeatmapSets);
                    beatmap.Details = localBeatmapDetails;
                });

                localBeatmaps.Maps.Add(beatmap);
            }

            return RefreshLocalPages(localBeatmaps);
        }

        private async Task<List<LocalBeatmapDetails>> GetLocalBeatmapDetails(LocalBeatmap localBeatmap, DifficultyBeatmapSet[] beatmapSets)
        {
            List<LocalBeatmapDetails> localBeatmapDetails = new List<LocalBeatmapDetails>();

            foreach (DifficultyBeatmapSet difficultyBeatmapSet in beatmapSets)
            {
                LocalBeatmapDetails beatmapDetails = new LocalBeatmapDetails(difficultyBeatmapSet.BeatmapCharacteristicName);

                foreach (DifficultyBeatmap difficultyBeatmap in difficultyBeatmapSet.DifficultyBeatmaps)
                {
                    float secondEquivalentOfBeat = 60 / localBeatmap.BeatsPerMinute;
                    float num4 = 1f;
                    float num5 = 18f;
                    float num6 = 4f;
                    float num8 = num6;

                    while (difficultyBeatmap.NoteJumpMovementSpeed * secondEquivalentOfBeat * num8 > num5)
                        num8 /= 2f;

                    float halfJumpDuration = num8 + difficultyBeatmap.NoteJumpStartBeatOffset;
                    if (halfJumpDuration < num4)
                        halfJumpDuration = num4;

                    string filePath = $@"{localBeatmap.FolderPath}\{difficultyBeatmap.BeatmapFilename}";
                    string json = await File.ReadAllTextAsync(filePath);
                    LocalBeatmapDetail beatmapDetail = JsonConvert.DeserializeObject<LocalBeatmapDetail>(json);
                    beatmapDetail.HalfJumpDuration = halfJumpDuration;
                    beatmapDetail.JumpDistance = difficultyBeatmap.NoteJumpMovementSpeed * (((float)secondEquivalentOfBeat) * (halfJumpDuration * 2));
                    if (beatmapDetail.Notes.Length > 0)
                        beatmapDetail.Duration = beatmapDetail.Notes[0].Time * secondEquivalentOfBeat;

                    beatmapDetail.DifficultyBeatmap = difficultyBeatmap;
                    beatmapDetails.BeatmapDetails.Add(beatmapDetail);
                }

                localBeatmapDetails.Add(beatmapDetails);
            }

            return localBeatmapDetails;
        }

        public void ChangeLocalPage(LocalBeatmaps localBeatmaps, int page)
        {
            if (page >= 0 && page <= localBeatmaps.LastPage)
            {
                if (page == 0)
                    localBeatmaps.PrevPage = null;
                else
                    localBeatmaps.PrevPage = page - 1;

                if (page == localBeatmaps.LastPage)
                    localBeatmaps.NextPage = null;
                else
                    localBeatmaps.NextPage = page + 1;
            }
            else if (page <= 0)
            {
                page = 0;
                localBeatmaps.PrevPage = null;
                if (page + 1 <= localBeatmaps.LastPage)
                    localBeatmaps.NextPage = page + 1;
                else
                    localBeatmaps.NextPage = null;
            }
            else if (page >= localBeatmaps.LastPage)
            {
                page = localBeatmaps.LastPage;
                localBeatmaps.NextPage = null;
                if (page - 1 >= 0)
                    localBeatmaps.PrevPage = page - 1;
                else
                    localBeatmaps.PrevPage = null;
            }
        }

        public LocalBeatmaps RefreshLocalPages(LocalBeatmaps localBeatmaps)
        {
            LocalBeatmaps newLocalBeatmaps = new LocalBeatmaps(localBeatmaps);
            int lastPage = 0;

            foreach (LocalBeatmap localBeatmap in newLocalBeatmaps.Maps)
            {
                int index = newLocalBeatmaps.Maps.IndexOf(localBeatmap);
                if (index > 0 && index % 10 == 0)
                    lastPage++;

                localBeatmap.Page = lastPage;
            }

            newLocalBeatmaps.LastPage = lastPage;
            if (lastPage == 0)
            {
                newLocalBeatmaps.NextPage = null;
                newLocalBeatmaps.PrevPage = null;
            }
            else
            {
                if (newLocalBeatmaps.NextPage is null && newLocalBeatmaps.PrevPage is null)
                {
                    if (lastPage >= 1)
                        newLocalBeatmaps.NextPage = 1;
                }
                else
                {
                    if (newLocalBeatmaps.NextPage is null)
                    {
                        if (newLocalBeatmaps.PrevPage < lastPage)
                        {
                            if (newLocalBeatmaps.PrevPage + 2 <= lastPage)
                                newLocalBeatmaps.NextPage = newLocalBeatmaps.PrevPage + 2;
                            else
                                newLocalBeatmaps.PrevPage = lastPage - 1;
                        }
                        else
                            newLocalBeatmaps.PrevPage = lastPage - 1;
                    }
                    else
                    {
                        if (newLocalBeatmaps.NextPage > lastPage)
                        {
                            newLocalBeatmaps.NextPage = null;
                            if (lastPage - 1 >= 0)
                                newLocalBeatmaps.PrevPage = lastPage - 1;
                        }
                    }
                }
            }

            return newLocalBeatmaps;
        }

        public async Task DownloadSong(OnlineBeatmap song)
        {
            string songName = song.Name;
            string levelAuthorName = song.Metadata.LevelAuthorName;

            foreach (string character in excludedCharacters)
            {
                songName = songName.Replace(character, "");
                levelAuthorName = levelAuthorName.Replace(character, "");
            }

            string downloadFilePath = $@"{downloadPath}\{song.Key}.zip";
            string downloadString = $"{beatSaver}{song.DownloadURL}";
            string extractPath = $@"{SongsPath}\{song.Key} ({songName} - {levelAuthorName})";

            if (!Directory.Exists(extractPath))
                Directory.CreateDirectory(extractPath);

            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add(HttpRequestHeader.UserAgent, "BeatSaverApi");
                song.IsDownloading = true;
                DownloadStarted?.Invoke(this, new DownloadStartedEventArgs(song));
                await webClient.DownloadFileTaskAsync(new Uri(downloadString), downloadFilePath);
                ZipFile.ExtractToDirectory(downloadFilePath, extractPath);
                File.Delete(downloadFilePath);

                DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs(song));
            }
        }

        public void DeleteSong(OnlineBeatmap song)
        {
            if (song.IsDownloaded)
            {
                string directory;
                if (Directory.Exists($@"{SongsPath}\{song.Hash}"))
                    directory = Directory.GetDirectories(SongsPath).FirstOrDefault(x => new DirectoryInfo(x).Name.Split(" ")[0] == song.Hash);
                else
                    directory = Directory.GetDirectories(SongsPath).FirstOrDefault(x => new DirectoryInfo(x).Name.Split(" ")[0] == song.Key);

                if (!string.IsNullOrEmpty(directory))
                    Directory.Delete(directory, true);

                song.IsDownloaded = false;
            }
        }

        public void DeleteSong(LocalBeatmap song)
        {
            string directory = Directory.GetDirectories(SongsPath).FirstOrDefault(x => new DirectoryInfo(x).Name.Split(" ")[0] == song.Identifier.Value);

            if (!string.IsNullOrEmpty(directory))
                Directory.Delete(directory, true);
        }

        public void DeleteSongs(ICollection<LocalBeatmap> songs)
        {
            foreach (LocalBeatmap song in songs)
                DeleteSong(song);
        }

        public async Task<OnlineBeatmap> GetBeatmap(LocalIdentifier identifier)
        {
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    webClient.Headers.Add(HttpRequestHeader.UserAgent, "BeatSaverApi");
                    string api = identifier.IsKey ? $"{beatSaverDetailsKeyApi}/{identifier.Value}" : $"{beatSaverDetailsHashApi}/{identifier.Value}";
                    string json = await webClient.DownloadStringTaskAsync(api);
                    return JsonConvert.DeserializeObject<OnlineBeatmap>(json);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
