// TextParser.cs - LITE VERSION
// Copyright (c) 2025 Spanky
//
// This script is part of the "Procedural Text Animator - LITE" pack.

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Globalization;

namespace ProceduralUIEffects.Lite
{
    public static class TextParser
    {
        public static (string, List<EffectRegion>, List<ActionMarker>) Parse(string rawText, EffectDatabase database)
        {
            if (database == null)
            {
                Debug.LogWarning("TextParser received a null database. No effects will be parsed.");
                return (rawText, new List<EffectRegion>(), new List<ActionMarker>());
            }

            database.Initialize();

            var cleanText = new StringBuilder();
            var effectRegions = new List<EffectRegion>();
            var actionMarkers = new List<ActionMarker>();
            var openRegions = new Dictionary<string, EffectRegion>();

            int cleanTextIndex = 0;

            for (int i = 0; i < rawText.Length; i++)
            {
                if (rawText[i] == '<')
                {
                    int endTagIndex = rawText.IndexOf('>', i);
                    if (endTagIndex == -1)
                    {
                        cleanText.Append(rawText[i]);
                        cleanTextIndex++;
                        continue;
                    }

                    string tagContent = rawText.Substring(i + 1, endTagIndex - i - 1);
                    i = endTagIndex;

                    int equalsIndex = tagContent.IndexOf('=');

                    if (tagContent.StartsWith("/"))
                    {
                        string tagName = tagContent.Substring(1);
                        if (openRegions.TryGetValue(tagName, out EffectRegion region))
                        {
                            region.endIndex = cleanTextIndex;
                            openRegions.Remove(tagName);
                            effectRegions.Add(region);
                        }
                    }

                    else if (equalsIndex != -1)
                    {
                        string[] parts = tagContent.Split('=');
                        string tagName = parts[0].ToLower().Trim();

                        if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                        {
                            actionMarkers.Add(new ActionMarker
                            {
                                Name = tagName,
                                Index = cleanTextIndex,
                                Value = value
                            });
                        }
                    }
                    else
                    {
                        string tagName = tagContent;
                        AnimationEffectBase effect = database.GetEffect(tagName);
                        if (effect != null)
                        {
                            var newRegion = new EffectRegion
                            {
                                effectAsset = effect,
                                startIndex = cleanTextIndex,
                                endIndex = int.MaxValue
                            };
                            openRegions[tagName] = newRegion;
                        }
                    }
                }
                else
                {
                    cleanText.Append(rawText[i]);
                    cleanTextIndex++;
                }
            }

            foreach (var region in openRegions.Values)
            {
                region.endIndex = cleanTextIndex;
                effectRegions.Add(region);
            }

            return (cleanText.ToString(), effectRegions, actionMarkers);
        }
    }
}