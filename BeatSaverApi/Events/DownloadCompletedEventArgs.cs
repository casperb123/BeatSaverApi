﻿using BeatSaverApi.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace BeatSaverApi.Events
{
    public class DownloadCompletedEventArgs : EventArgs
    {
        public OnlineBeatmap Song { get; set; }

        public DownloadCompletedEventArgs(OnlineBeatmap song)
        {
            Song = song;
        }
    }
}
