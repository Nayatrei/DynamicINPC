// NewsItemEntry.cs (can be in its own file or within another relevant script)
using UnityEngine;
using System;
using System.Collections.Generic;

namespace CelestialCyclesSystem
{
    [CreateAssetMenu(fileName = "NewsItemEntry", menuName = "Celestial NPC/News Item Entry")]
    public class iTalkNewsItemEntry : ScriptableObject
    {
        public List<string> texts = new List<string>();
        public List<long> timestamps = new List<long>();

        // Optional: Add a single news item
        public void AddNews(string text, long timestamp)
        {
            texts.Add(text);
            timestamps.Add(timestamp);
        }
    }
}