﻿using BeatSaverApi.Entities;
using System;

namespace BeatSaverApi.Events
{
    public class DownloadCompletedEventArgs : EventArgs
    {
        public OnlineBeatmap Beatmap { get; private set; }

        public DownloadCompletedEventArgs(OnlineBeatmap beatmap)
        {
            Beatmap = beatmap;
        }
    }
}
